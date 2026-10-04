# ADR 0009 — Identité, authentification et sessions

- **Statut** : acceptée — 2026-09-24
- **Remplace** : Symfony Security, sessions PHP en fichiers, admin unique défini par
  variables d'environnement

## Contexte

- Hash de mots de passe Symfony `auto` : **bcrypt (`$2y$`) ou argon2id (`$argon2id$`)**
  selon ce que `auto` a retenu. ASP.NET Core Identity (PBKDF2) ne sait lire ni l'un ni
  l'autre.
- Les sessions PHP sont stockées en fichiers. Leur verrou **sérialise les requêtes
  concurrentes** d'un même utilisateur : il a fallu des `session->save()` et des
  `session_write_close()` manuels.
- L'anti-bruteforce et les rate limiters vivent dans un pool de cache fichier, **remis à
  zéro à chaque déploiement**.
- L'admin est un utilisateur unique défini par variables d'environnement : pas de rôles,
  pas de MFA, un seul admin possible.
- Les apps (desktop, Android) ne peuvent pas s'appuyer sur un cookie de session du
  site : leur origine diffère (`https://localhost`, loopback).
- Règles actuelles à conserver :
  - politique de mot de passe CNIL : 12 caractères, 4 classes, liste des 1 000 mots de
    passe les plus courants ;
  - « se souvenir de moi » 30 jours ;
  - vérification d'e-mail et réinitialisation : lien signé valable 1 h, usage unique ;
  - bannissement ;
  - suppression de compte en trois variantes ;
  - Google OAuth qui ne lie un compte existant que si Google atteste l'e-mail.

## Décision

### ASP.NET Core Identity sur la table `users` existante

- Stores EF Core mappés sur `users`. Les colonnes propres à Identity (security stamp,
  lockout, compteur d'échecs…) sont ajoutées par **migration additive, nullable ou avec
  valeur par défaut**. L'ancienne stack peut donc encore tourner sur le même schéma
  pendant la bascule.
- Unicité insensible à la casse conservée (`LOWER(email)`, `LOWER(username)`), et
  connexion par e-mail ou nom d'utilisateur.

### Mots de passe : migration transparente, retour arrière possible

- `IPasswordHasher<User>` sur mesure :
  - **vérifie** les hash hérités : bcrypt via BCrypt.Net-Next, argon2i et argon2id au
    format encodé PHC ;
  - répond `SuccessRehashNeeded`, ce qui re-hache le mot de passe à la connexion ;
  - produit les **nouveaux hash en argon2id au format PHC standard**, avec au minimum
    les paramètres OWASP (m = 19 Mio, t = 2, p = 1).
- Pourquoi argon2id plutôt que le PBKDF2 d'Identity :
  - c'est la recommandation OWASP de premier rang, alors que le PBKDF2 d'Identity
    (100 000 itérations) est sous le seuil OWASP ;
  - surtout, **`password_verify` de PHP lit ce format** : un retour arrière vers
    l'ancienne stack reste possible pendant la bascule sans réinitialiser aucun compte.
    Un **test croisé** (hash .NET vérifié par PHP) le garantit.
- Politique CNIL portée en validateurs Identity, avec les mêmes messages et la même
  checklist en direct.

### Deux schémas d'authentification, aucune session serveur

| Client | Mécanisme |
|---|---|
| Web | cookie Identity `__Host-`, `HttpOnly`, `Secure`, `SameSite=Lax` ; « se souvenir de moi » 30 jours ; **antiforgery** sur les requêtes non sûres (convention `XSRF-TOKEN` de `HttpClient`) + contrôle de l'`Origin` |
| Desktop | jetons bearer + refresh, **détenus par l'hôte .NET** et jamais exposés au JavaScript (proxy loopback, ADR 0007) ; refresh chiffré au repos (Data Protection locale) |
| Android | jetons bearer + refresh, refresh dans le **stockage sécurisé (Android Keystore)** |
| API publique `/v1` | clé d'API (schéma séparé, ADR 0002) |

- Jetons : endpoints Identity (`MapIdentityApi`) pour nos clients de première partie.
  **OpenIddict** n'entre que si des clients OAuth tiers apparaissent : l'API publique
  reste sur des clés.
- **Pas de session serveur.** La version et la locale sont dans l'URL, le thème et la
  variante de langue dans des cookies. Plus aucun verrou ne sérialise les requêtes.
- **Clés Data Protection persistées en Postgres**, chiffrées au repos par un certificat
  fourni en secret. Un déploiement ne déconnecte plus personne, et N instances partagent
  les mêmes clés.

### Protections

- **Lockout Identity** (5 échecs → 15 min), stocké en base : il survit aux déploiements.
- Rate limiters par IP sur l'inscription, la réinitialisation, le contact et le checkout
  (pas sur les actions authentifiées, pour éviter les faux positifs derrière un NAT).
- Bannissement : vérifié à la connexion **et** lors de la revalidation périodique du
  security stamp (quelques minutes). Il prend donc effet sur les sessions déjà
  ouvertes. Les builds d'un banni sortent des tendances, mais leurs liens de partage
  restent valides.
- E-mails de vérification et de réinitialisation : jetons Identity de 1 h, à usage
  unique via le security stamp. Envoi par l'**outbox** (ADR 0003), dans la **locale de
  l'utilisateur** passée explicitement (plus de traducteur lié à la requête).

### Google OAuth

- **Web** : flux de redirection standard (`AddGoogle`). Même règle de liaison qu'avant :
  on ne lie un compte existant que si `email_verified` est vrai, sinon on crée un compte
  sans mot de passe.
- **Apps** : navigateur système + PKCE, puis échange contre nos jetons. Google refuse les
  connexions depuis une WebView embarquée (`disallowed_useragent`). Le retour se fait :
  - sur Android, par un **App Link** vérifié (`assetlinks.json` enfin rempli) ;
  - sur desktop, par une **redirection loopback** vers le Kestrel local (RFC 8252).

### Admin

- **Rôle `Admin` dans Identity**, **MFA TOTP obligatoire**, plusieurs admins possibles.
- Premier admin créé par une commande CLI, plus par variables d'environnement.
- Journal d'audit : acteur polymorphe, ensemble d'actions fermé, best effort (ne fait
  jamais échouer l'action auditée).

## Alternatives écartées

| Alternative | Pourquoi non |
|---|---|
| Hasher PBKDF2 d'Identity + réinitialisation forcée | Friction pour tous les comptes, et retour arrière impossible |
| OpenIddict dès maintenant | Serveur OAuth complet sans client tiers pour l'utiliser |
| Cookies pour les apps | Origines différentes, cookies tiers : fragile et bloqué sur mobile |
| Sessions distribuées (Redis) | Recrée l'état serveur que la décision élimine |

## Conséquences

- **+** Plus de verrou de session, plus de déconnexion au déploiement, anti-bruteforce
  persistant.
- **+** Admin multiple avec MFA.
- **+** Migration des mots de passe invisible pour les joueurs, avec retour arrière
  possible.
- **−** Deux schémas d'authentification (cookie et jetons) à tester chacun.
- **−** Les App Links Android et la redirection loopback du desktop ajoutent de la
  configuration par plateforme.
