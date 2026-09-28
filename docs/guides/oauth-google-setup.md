# Connexion Google (« Sign in with Google ») — guide opérateur

Ce guide couvre **toutes les étapes manuelles côté Google Cloud Console** (interface
« Google Auth Platform », état 2026) puis l'endroit exact où poser les identifiants
dans ce dépôt. Sans ces étapes, la fonctionnalité reste inactive et se dégrade
proprement : le bouton « Continuer avec Google » renvoie vers la page de connexion
avec un message « connexion Google non configurée ».

Côté code, tout est déjà en place dans l'API (`LoDb.Api`, module `Accounts`) : retour de
Google sur `/api/account/google/callback`, échange de code pour les apps desktop et
Android. Scopes demandés : `openid`, `profile`, `email` — **non sensibles**, ce qui permet
une publication sans revue Google (voir plus bas). Les routes `/connect/google…` de
l'ancienne stack (archivée sous `legacy/`) restent déclarées chez Google jusqu'à la
décommission, pour le retour arrière.

## 1. Créer le projet Google Cloud

1. Aller sur <https://console.cloud.google.com/> et se connecter avec le compte
   Google « propriétaire » de l'intégration (compte d'équipe de préférence).
2. Bandeau supérieur → sélecteur de projet → **New project**. Nom suggéré :
   `league-of-data-base` (l'ID de projet est libre, il n'apparaît pas aux joueurs).
3. Aucune facturation n'est requise pour OAuth.

## 2. Configurer Google Auth Platform (écran de consentement)

1. Ouvrir <https://console.cloud.google.com/auth/overview> (menu : **APIs & Services
   → OAuth consent screen**, renommé « Google Auth Platform »).
2. Premier passage : cliquer **Get started**. Le formulaire se déroule en 4 étapes :
   - **App Information** : *App name* affiché sur l'écran Google (ex.
     `League Of Data Base`) + *User support email*.
   - **Audience** : choisir **External** (les joueurs se connectent avec n'importe
     quel compte Google ; *Internal* est réservé aux organisations Workspace).
   - **Contact Information** : e-mail(s) de contact développeur (notifications Google).
   - **Finish** : accepter la *User Data Policy* de Google et valider **Create**.

### Branding (logo) — ⚠️ piège

Page <https://console.cloud.google.com/auth/branding> : app name, domaines, logo.

**Ne pas téléverser de logo au début.** Ajouter un logo déclenche la
*brand verification* (revue manuelle par Google, plusieurs jours, questionnaire),
alors que sans logo l'app peut être publiée immédiatement. L'écran de consentement
affichera le nom seul — suffisant. Ajouter le logo plus tard, une fois le flux validé
en production, si l'écran « brut » dérange.

### Audience : Testing → In production

Page <https://console.cloud.google.com/auth/audience> :

- En statut **Testing** : seuls les comptes ajoutés dans **Test users** (100 max)
  peuvent se connecter, et Google affiche un écran « Cette application n'a pas été
  validée » aux autres.
- Cliquer **Publish app** pour passer **In production**. Comme l'app ne demande que
  des scopes **non sensibles** (`openid`, `email`, `profile`), la publication est
  **immédiate et sans revue** : pas de vérification, pas de questionnaire
  (référence : <https://support.google.com/cloud/answer/9110914>). L'avertissement
  « unverified app » disparaît pour tout le monde.

## 3. Créer le client OAuth (identifiants)

1. Ouvrir <https://console.cloud.google.com/auth/clients> → **Create client**.
2. **Application type** : **Web application**. Nom interne libre (ex. `lodb-web`).
3. **Authorized JavaScript origins** — origine exacte, sans chemin, une par
   environnement : `https://league-of-data-base.com` (`prod`) et
   `https://test.league-of-data-base.com` (`staging`).
4. **Authorized redirect URIs** — doivent correspondre **exactement** (schéma, hôte,
   chemin) à ce que l'API envoie :
   - `https://league-of-data-base.com/api/account/google/callback`
   - `https://test.league-of-data-base.com/api/account/google/callback`
   - garder les URI `…/connect/google/check` de l'ancienne stack (sur les deux domaines)
     jusqu'à la décommission : le retour arrière en a besoin.
5. **Create** → récupérer immédiatement le **Client ID**
   (`xxxxxxxx.apps.googleusercontent.com`) et le **Client secret** (affiché une
   seule fois ; régénérable depuis la fiche du client au besoin).
6. Apps : un client **Desktop app** pour le desktop (retour en boucle locale), et un
   client pour Android (retour par l'App Link `https://<hôte>/app/oauth/google`). Leurs
   ids sont déclarés côté serveur (§ 4) ; celui du desktop est aussi compilé dans l'app
   (propriété MSBuild `LoDbDesktopGoogleClientId`).

En local, la connexion Google n'est pas couverte : nginx transmet `Host` sans le port, et
l'URI de retour calculée par l'API perd `:18080`.

Références Google : flux serveur
<https://developers.google.com/identity/protocols/oauth2/web-server>, OpenID Connect
<https://developers.google.com/identity/openid-connect/openid-connect>, gestion des
clients <https://support.google.com/cloud/answer/15544987> et écran de consentement
<https://support.google.com/cloud/answer/15549945>.

## 4. Poser les identifiants

Les identifiants sont des **secrets** : ils ne vont jamais dans le dépôt, mais dans le
`.env` de l'environnement servi (`.env.staging`, `.env.prod`), c'est-à-dire dans le secret
GitHub `ENV_STAGING` ou `ENV_PROD` ([`configuration.md`](configuration.md), § 4.2) :

```dotenv
LoDb__Accounts__Google__ClientId=xxxxxxxx.apps.googleusercontent.com
LoDb__Accounts__Google__ClientSecret=GOCSPX-...
# Apps : desktop (id + secret), Android (id seul) ; jamais sans le client web
LoDb__Accounts__Google__AppClients__0__ClientId=yyyyyyyy.apps.googleusercontent.com
LoDb__Accounts__Google__AppClients__0__ClientSecret=GOCSPX-...
LoDb__Accounts__Google__AppClients__1__ClientId=zzzzzzzz.apps.googleusercontent.com
```

`compose.deploy.yaml` transmet ces lignes à l'API quand elles sont présentes ; le
déploiement suivant les prend en compte. Sans `ClientId`, la connexion Google est coupée et
les points d'entrée répondent `google-unavailable`. En prod, ce sont les identifiants
`OAUTH_GOOGLE_CLIENT_ID` et `OAUTH_GOOGLE_CLIENT_SECRET` de l'ancienne stack : même client.

## 5. Piège production : HTTPS derrière le proxy TLS

L'URI de retour envoyée à Google est calculée par l'API à partir de la requête. Derrière
l'edge TLS (Caddy → nginx → API), si l'API ne faisait pas confiance aux en-têtes
`X-Forwarded-*`, elle verrait du HTTP et enverrait `http://…/api/account/google/callback`
→ erreur `redirect_uri_mismatch`.

**Déjà configuré** : nginx ne croit `X-Forwarded-For` que de l'edge (`LODB_EDGE_CIDR`) et
transmet le schéma à l'API, qui accepte ces en-têtes des réseaux privés
(`LoDb__Hosting__KnownProxyNetworks__*`, fixés par `compose.yaml`). Si l'edge change,
vérifier que le nouvel intermédiaire émet bien `X-Forwarded-Proto: https`.

## 6. Comportement applicatif (rappel, ancienne stack)

> Décrit l'ancienne stack ; celui de la nouvelle est fixé par l'ADR 0009
> ([`0009-identite-authentification-sessions.md`](../reecriture/adr/0009-identite-authentification-sessions.md)).

- **Compte existant avec le même e-mail** : rattaché au premier login Google
  **uniquement si** Google atteste `email_verified` (anti-takeover) ; le
  `googleId` (claim `sub`, stable) est alors mémorisé.
- **Nouveau compte** : créé sans mot de passe, pseudo URL-safe généré depuis le
  prénom Google ou la partie locale de l'e-mail (suffixe numérique si collision).
  L'utilisateur peut définir un mot de passe ensuite sur `/profile`.
- **Denylist mots de passe** : locale (embarquée dans le dépôt). Un contrôle type
  HIBP (k-anonymity) nécessiterait un egress HTTP — interdit depuis PHP par
  l'architecture ; si souhaité un jour, il devra passer par le go-fetcher
  (ajout de `api.pwnedpasswords.com` à son allowlist).
