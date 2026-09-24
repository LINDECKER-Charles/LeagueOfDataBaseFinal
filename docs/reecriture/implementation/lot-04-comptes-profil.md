# Lot 4 — Comptes et profil

Objectif du lot : ASP.NET Core Identity sur la table `users` existante, sans session
serveur, avec une migration des mots de passe invisible et réversible ; les contrats
d'outbox et d'audit dont tous les lots suivants se servent ; le socle d'authentification du
front et toutes les pages de compte et de profil.

Critère de sortie local : connexion de comptes bcrypt et argon2 (hash au format produit par
Symfony) ; hash ré-écrit vérifié par `password_verify` de PHP. La connexion avec des comptes
réels du dump de répétition est une opération hôte (lot 8).

Ordre : L4.1, puis L4.2 et L4.3 en parallèle, puis L4.4 et L4.5 (après L4.2), puis L4.6.

## L4.1 — Schéma Identity, hasher, politique de mot de passe, contrats outbox et audit

- **Objectif** : Identity branché sur `users` par une migration **additive**, un hasher qui
  lit les hash hérités et produit de l'argon2id lisible par PHP, la politique CNIL, les clés
  Data Protection en base, et les contrats de l'outbox et de l'audit.
- **À lire** : `app/src/Entity/User.php` et `Entity/Concern/*`,
  `app/config/packages/{security,reset_password}.yaml`, `app/src/Security/**`,
  `app/src/Validator/{CnilPassword,CnilPasswordValidator}.php` et
  `Validator/Resources/common-passwords.txt`, `app/src/Form/PasswordFieldOptions.php`,
  `app/assets/vue/security/passwordRules.ts`, `app/src/Service/Audit/**` (dont
  `Model/Action.php`), ADR [0009](../adr/0009-identite-authentification-sessions.md),
  `plan-migration.md` (expand/contract).
- **Périmètre** : migration, entités et configurations du lot 4 dans
  `src/LoDb.Infrastructure/Persistence/**` ; `src/LoDb.Infrastructure/DataProtection/**` ;
  `src/LoDb.Infrastructure/Audit/**` ; les fichiers de contrat de l'outbox
  (`src/LoDb.Infrastructure/Outbox/{IEmailOutbox,EmailMessage,EmailTemplate}.cs`) ;
  `src/LoDb.Api/Modules/Accounts/Security/**` ; `tests/fixtures/hashes/**` ; compléments
  de l'anonymisation (`tools/next/db/`) ; tests associés.
- **Conception** :
  - `User : IdentityUser<int>` sur `users` : `email`, `username`, `password` (nullable pour
    les comptes Google), `is_verified` pour `EmailConfirmed` ; les propriétés Identity sans
    colonne (téléphone…) sont ignorées. Les longueurs héritées sont **reconfigurées**
    (`email` 180, `username` 24) : la configuration de base d'Identity les élargirait à
    256, ce qui casserait la règle additive et la comparaison avec `Baseline`. La colonne
    JSON `roles` de Symfony reste, non utilisée.
  - Colonnes ajoutées, nullables ou avec défaut : `security_stamp`, `concurrency_stamp`,
    `lockout_end` (`timestamptz`), `lockout_enabled`, `access_failed_count`,
    `two_factor_enabled`. `normalized_email` et `normalized_username` sont des **colonnes
    générées** (`upper(email)`, `upper(username)`, `STORED`, ignorées en écriture par EF) :
    toujours justes, même pour un compte créé par l'ancienne stack pendant la période de
    retour arrière.
  - Un `security_stamp` NULL (compte écrit par l'ancienne stack) est régénéré à la
    connexion suivante, jamais une erreur.
  - Rôles dans des tables `identity_roles` / `identity_user_roles` (noms distincts de la
    colonne `roles`) ; jetons dans `identity_user_tokens`. Google reste porté par
    `users.google_id`, que l'ancienne stack lit aussi.
  - Hasher : vérifie bcrypt `$2y$` (BCrypt.Net-Next), argon2i et argon2id au format PHC ;
    répond `SuccessRehashNeeded` pour tout format hérité ou paramètres plus faibles ;
    produit de l'argon2id PHC, au minimum m = 19 Mio, t = 2, p = 1 (valeurs finales
    mesurées et justifiées au compte rendu).
  - Politique CNIL en validateurs Identity : 12 caractères, minuscule, majuscule, chiffre,
    caractère spécial, liste des mots de passe courants (997 entrées, comparaison en
    minuscules), 4096 caractères au plus ; mêmes clés de message qu'aujourd'hui.
  - Verrouillage : 5 échecs → 15 minutes, stocké en base.
  - Data Protection : table `data_protection_keys`, clés chiffrées par un certificat fourni
    en secret (`LoDb:DataProtection:*`) ; en dev, sans chiffrement et avec un avertissement.
  - Outbox : table `email_outbox` (destinataire, gabarit, locale, modèle JSONB, statut,
    tentatives, prochaine tentative, dates, dernier code d'erreur) et contrat
    `IEmailOutbox.EnqueueAsync(EmailMessage)` avec l'énumération des gabarits
    (`ConfirmEmail`, `ResetPassword`, `ContactNotification`) ; L4.3 l'implémente.
  - Audit : table `audit_log` (date, type et id d'acteur, acteur, action, résultat, cible,
    IP, route, méta JSONB ; index par acteur et par action) ; `IAuditLog` en **best effort**
    (n'échoue jamais l'action auditée ; l'échec se journalise) ; ensemble fermé des 24
    actions actuelles ; acteur polymorphe (utilisateur, admin, anonyme) ; miroir dans les
    logs (`audit.*`) **sans `ip` ni `meta.identifier`**. Chaque chantier de module ajoute
    ses propres appels à partir d'ici.
- **Tests** : fixtures de hash produits par PHP (bcrypt au coût Symfony, argon2id aux
  paramètres par défaut de PHP) vérifiés par .NET ; hash .NET vérifiés par
  `password_verify` dans un conteneur `php:8.5-cli` lancé par Testcontainers (test croisé,
  sans dépendance à l'hôte) ; validateurs règle par règle ; lecture d'un utilisateur inséré
  en SQL comme le ferait l'ancienne stack ; colonnes générées ; tampon NULL ; verrouillage ;
  migration sans changement des colonnes héritées ; audit : ensemble fermé, base
  indisponible sans exception remontée, miroir filtré.
- **Dépend de** : lot 1. **Taille** : L.

## L4.2 — Authentification web et jetons, Google, protections

- **Objectif** : tous les parcours d'authentification pour le web (cookie) et pour les apps
  (jetons), sans session serveur ni verrou de session.
- **À lire** : `app/src/Controller/Account/{SecurityController,RegistrationController,
  EmailVerificationController,ResetPasswordController,GoogleConnectController}.php`,
  `app/src/Security/{GoogleAuthenticator,GoogleAccountProvisioner,UserChecker,
  UsernameAllocator}.php`, `app/src/Service/Tools/UrlGenerator.php` (retour sûr),
  `app/config/packages/{security,framework,csrf,reset_password}.yaml` (rate limiters,
  CSRF, limite de réinitialisation), [`oauth-google-setup.md`](../../guides/oauth-google-setup.md),
  `heritage.md` A4, G5, G6.
- **Périmètre** : `src/LoDb.Api/Modules/Accounts/**` (hors `Security/` et `Mail/`),
  `src/LoDb.Api/Hosting/{RateLimitingPolicies,AuthorizationPolicies}.cs`, tests associés.
- **Conception** :
  - Endpoints propres (écart à `MapIdentityApi` consigné au §13 du plan) :
    `/api/account/register` (nom d'utilisateur `^[a-zA-Z0-9][a-zA-Z0-9_.-]{2,23}$`, e-mail
    en minuscules, unicité insensible à la casse, e-mail de vérification par
    `IEmailOutbox`), `login` (e-mail ou nom d'utilisateur), `logout`, `me` (résumé pour
    l'hydratation) ; cookie `__Host-` `HttpOnly`, `Secure`, `SameSite=Lax` ; « se souvenir
    de moi » 30 jours.
  - Vérification d'e-mail et réinitialisation : jetons Identity d'une heure, à usage unique
    par le security stamp ; renvoi limité à 3 par 15 minutes et par utilisateur ; une
    demande de réinitialisation par heure et par compte, comme aujourd'hui ; aucune fuite
    d'existence du compte.
  - Retour après connexion ou action : seulement vers le même hôte (`Referer` ou
    `returnUrl` d'un autre hôte ignorés).
  - Google web : défi standard, `CallbackPath` sous `/api/account/google/callback` (nginx
    route `/api` vers l'API ; `/signin-google` partirait au SSR) ; liaison à un compte
    existant seulement si `email_verified`, sinon compte sans mot de passe ; nom
    d'utilisateur alloué comme `UsernameAllocator`.
  - Apps : jetons bearer + refresh (`/api/account/token`, `/api/account/refresh`) et échange
    Google par PKCE (`/api/account/google/app/exchange`) pour L9 et L10.
  - Antiforgery sur les requêtes non sûres authentifiées par cookie (cookie `XSRF-TOKEN`,
    en-tête `X-XSRF-TOKEN`) et contrôle de l'`Origin`.
  - Politiques de rate limiting nommées, par IP réelle (§5.1 du plan) : inscription 5/h,
    réinitialisation 5/h, contact 5/h, checkout de don 10/h (aucune sur les actions
    authentifiées).
  - Politiques d'autorisation nommées : `Authenticated`, `VerifiedEmail`, `Admin` (rôle +
    MFA, utilisée au lot 7).
  - Bannissement : refusé à la connexion par toutes les méthodes, jetons compris ; le
    changement de tampon et la revalidation périodique (quelques minutes) le font appliquer
    aux sessions ouvertes.
  - Appels d'audit des actions de compte (connexion, échec, déconnexion, inscription…).
- **Tests** : chaque endpoint en nominal et en erreur ; doublons insensibles à la casse ;
  connexion bannie refusée ; verrouillage après 5 échecs ; expiration du cookie « se
  souvenir de moi » ; XSRF manquant ou `Origin` étrangère refusés ; 429 par IP et limite
  par compte ; retour vers un autre hôte refusé ; règles de liaison Google (fournisseur OIDC
  simulé) ; refresh ; bannissement effectif sur une session ouverte ; événements d'audit.
- **Dépend de** : L4.1. **Taille** : L.

## L4.3 — Outbox e-mail et gabarits localisés

- **Objectif** : des envois fiables hors requête, dans la locale explicite du destinataire.
- **À lire** : `app/src/Service/Mail/**`, `app/templates/email/**`, clés `email.*` de
  `app/translations/messages.*.yaml`, `app/config/packages/mailer.yaml`, `heritage.md` I3.
- **Périmètre** : `src/LoDb.Infrastructure/Outbox/**` (hors fichiers de contrat de L4.1 ;
  dont le fichier d'enregistrement `Outbox`), `src/LoDb.Api/Workers/Outbox/**`, tests
  associés.
- **Conception** : implémentation de `IEmailOutbox` dans la même transaction que le
  changement métier ; travailleur qui prend des lots par `FOR UPDATE SKIP LOCKED`, backoff
  exponentiel, statut mort après N tentatives ; SMTP par MailKit (`LoDb:Mail:*`, Mailpit en
  dev), expéditeur `MAILER_FROM` ; gabarits confirmation, réinitialisation et notification
  de contact, en HTML et en texte, textes `email.*` en ressources serveur (repli `en`) ;
  sujet du contact inchangé (`[Contact · <catégorie>] …`, avec Reply-To du visiteur) ; logs
  sans adresse e-mail.
- **Tests** : mise en file transactionnelle ; envoi réel vers un Mailpit lancé par
  Testcontainers ; reprise et backoff ; statut mort ; choix de la locale et repli ;
  instantanés des gabarits en `en` et `fr`.
- **Dépend de** : L4.1. **Taille** : M.

## L4.4 — Profil, favoris, profil public, suppression, bannissement (API)

- **Objectif** : toutes les règles du profil et de la confidentialité côté serveur.
- **À lire** : `app/src/Controller/Account/{ProfileController,ProfileCurationController,
  AccountSecurityController,PublicProfileController}.php`, `app/src/Service/Profile/**`,
  `app/src/Entity/Concern/{PinsProfileFavorites,TracksModerationBan}.php`, `heritage.md`
  § 6 (Favoris, Confidentialité).
- **Périmètre** : `src/LoDb.Api/Modules/Profiles/**`, tests associés.
- **Conception** :
  - `GET /api/profile` : e-mail masqué, identité, visibilité, favoris résolus sur la
    version épinglée (`preferred_version`), bannière de skin.
  - `GET /api/profile/preview` : la carte publique telle qu'elle apparaîtrait, visible par
    son propriétaire même quand le profil est privé.
  - `PUT /api/profile/favorites` (quatre emplacements + skin) : validés contre le catalogue
    de la version épinglée ; un favori indisponible n'est **jamais** effacé ; un échec de
    données refuse la sauvegarde.
  - `PUT /api/profile/version`, `PUT /api/profile/identity` (nom d'utilisateur, tagline Riot
    `^[A-Za-z0-9]{3,5}$`), `PUT /api/profile/password` (définition d'un mot de passe pour un
    compte qui n'en a pas), suppression du compte dans ses trois variantes (lues dans
    `AccountSecurityController`).
  - `GET /api/profiles/{username}` : même 404 pour un profil inconnu, privé ou banni ;
    builds publics du profil.
  - Appels d'audit des actions de profil.
- **Tests** : résolution des favoris, aperçu d'un profil privé par son propriétaire et 404
  pour les autres, absence d'oracle sur les 404, variantes de suppression, validation de
  l'identité, exclusion des bannis.
- **Dépend de** : L4.2, lot 2. **Taille** : M.

## L4.5 — Socle d'authentification front

- **Objectif** : ce que toutes les pages et toutes les plateformes partagent pour savoir qui
  est connecté et authentifier leurs requêtes.
- **À lire** : ADR 0006 (couche plateforme) et 0009 (schémas d'authentification), §5.2 du
  plan (stratégies, contrat de l'hôte desktop).
- **Périmètre** : `src/LoDb.Web/src/app/core/auth/**`.
- **Conception** : session lue sur `/api/account/me` (signal, rafraîchie après connexion ou
  déconnexion, jamais pendant un rendu SSR : le SSR reste anonyme) ; gardes
  `authenticated`, `verifiedEmail`, `admin` ; interface `AuthStrategy` (connexion,
  déconnexion, session, préparation des requêtes) choisie par `PlatformService` ; stratégie
  `cookie` pour le web (XSRF géré par `HttpClient`, `safeReturnUrl` de L3.1 après
  connexion) ; intercepteur qui délègue à la stratégie. Les stratégies `host` (L9.3) et
  `bearer` (L10.2) s'y branchent sans le modifier.
- **Tests** : session et gardes ; stratégie cookie ; intercepteur avec une stratégie
  simulée.
- **Dépend de** : L4.2, fondations du lot 3. **Taille** : M.

## L4.6 — Pages compte et profil

- **Objectif** : les pages de compte et de profil, et les éléments du gabarit liés au
  compte.
- **À lire** : `app/templates/security/**`, `register.html.twig`,
  `reset_password_*.html.twig`, `app/templates/profile/**`,
  `app/assets/vue/components/account/{FavoritePicker,SkinBannerPicker,
  PasswordChecklist}.vue`, `app/assets/vue/{picker,profile,security}/**`, leurs specs,
  `app/assets/styles/account/**`.
- **Périmètre** : `src/LoDb.Web/src/app/features/{account,profile}/**` (dont les composants
  provisoires `account-menu` et `verify-email-banner` créés par L3.1),
  `tests/LoDb.E2E/specs/{account,profile}/**`.
- **Conception** : connexion, inscription, vérification, mot de passe oublié et
  réinitialisation, avec la checklist de mot de passe en direct (mêmes règles que le
  serveur) ; bouton Google en navigation pleine page ; menu du compte ; bandeau d'e-mail non
  vérifié avec renvoi ; profil : favoris en emplacements, choix de la bannière (champion
  puis skin), identité, visibilité, définition du mot de passe, suppression ; aperçu de la
  carte publique ; sauvegarde automatique différée ; profil public `/{locale}/u/{username}`
  rendu en SSR anonyme, avec ProfilePage et ses builds publics.
- **Tests** : magasin du formulaire de profil, règles de mot de passe et filtres du picker
  (cas des specs repris) ; E2E des parcours d'inscription, de connexion, de profil et du
  profil public.
- **Dépend de** : L4.4, L4.5. **Taille** : L.
