# Connexion Google (« Sign in with Google ») — guide opérateur

Ce guide couvre **toutes les étapes manuelles côté Google Cloud Console** (interface
« Google Auth Platform », état 2026) puis l'endroit exact où poser les identifiants
dans ce dépôt. Sans ces étapes, la fonctionnalité reste inactive et se dégrade
proprement : le bouton « Continuer avec Google » renvoie vers la page de connexion
avec un message « connexion Google non configurée ».

Côté code, tout est déjà en place : routes `/connect/google` (départ) et
`/connect/google/check` (callback), authenticator sur le firewall `main`,
provisionnement de compte (rattachement par e-mail vérifié ou création avec
pseudo généré). Scopes demandés : `openid`, `profile`, `email` — **non sensibles**,
ce qui permet une publication sans revue Google (voir plus bas).

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
3. **Authorized JavaScript origins** — origine exacte, sans chemin :
   - `http://localhost:8080` (dev, port nginx publié par `compose.override.yaml`)
   - `https://<domaine-prod>` (ex. `https://leagueofdatabase.example`)
4. **Authorized redirect URIs** — doivent correspondre **exactement** (schéma,
   hôte, port, chemin) à la route `connect_google_check` :
   - `http://localhost:8080/connect/google/check`
   - `https://<domaine-prod>/connect/google/check`
5. **Create** → récupérer immédiatement le **Client ID**
   (`xxxxxxxx.apps.googleusercontent.com`) et le **Client secret** (affiché une
   seule fois ; régénérable depuis la fiche du client au besoin).

Références Google : flux serveur
<https://developers.google.com/identity/protocols/oauth2/web-server>, OpenID Connect
<https://developers.google.com/identity/openid-connect/openid-connect>, gestion des
clients <https://support.google.com/cloud/answer/15544987> et écran de consentement
<https://support.google.com/cloud/answer/15549945>.

## 4. Poser les identifiants dans ce dépôt

Les deux variables (déclarées vides dans `app/.env`, bloc `###> google oauth ###`)
sont des **secrets** : ne jamais les committer.

| Variable | Contenu |
|---|---|
| `OAUTH_GOOGLE_CLIENT_ID` | Client ID `…apps.googleusercontent.com` |
| `OAUTH_GOOGLE_CLIENT_SECRET` | Client secret associé |

Chaîne de propagation (identique à Stripe/MinIO) :

1. **`.env` à la racine du dépôt** (git-ignoré — celui lu par `docker compose`,
   pas `app/.env`) :

   ```dotenv
   OAUTH_GOOGLE_CLIENT_ID=xxxxxxxx.apps.googleusercontent.com
   OAUTH_GOOGLE_CLIENT_SECRET=GOCSPX-...
   ```

2. `compose.yaml` mappe déjà ces variables dans l'environnement du service `php`
   (`OAUTH_GOOGLE_CLIENT_ID: ${OAUTH_GOOGLE_CLIENT_ID:-}` + secret) ; elles sont
   consommées par `config/packages/knpu_oauth2_client.yaml` et les classes
   `GoogleConnectController` / `GoogleAuthenticator` via `%env()%` / `#[Autowire(env:)]`.
3. Recréer le conteneur pour prise en compte :

   ```bash
   docker compose up -d php
   ```

4. En CI/CD, ajouter les deux secrets au même endroit que les autres
   (cf. `docs/github-actions-secrets.md`).

Vérification rapide : `curl -sI http://localhost:8080/connect/google` doit renvoyer
un `302` vers `accounts.google.com` (variables posées) ou vers `/login` (variables
vides — dégradation propre).

## 5. Piège production : HTTPS derrière le proxy TLS

La `redirect_uri` envoyée à Google est **générée par Symfony** à partir de la
requête. Derrière l'edge TLS (Caddy → nginx → php-fpm), si les en-têtes
`X-Forwarded-*` n'étaient pas relayés/acceptés, Symfony verrait du HTTP et
générerait `http://…/connect/google/check` → erreur `redirect_uri_mismatch`
(l'URI autorisée est en `https://`).

**État réel de ce dépôt : déjà configuré, rien à faire.**

- Caddy (edge, `compose.deploy.yaml` + réseau `edge`) pose automatiquement
  `X-Forwarded-Proto/For/Host` en proxifiant vers `nginx:80`.
- nginx transmet ces en-têtes à php-fpm (les en-têtes de requête passent en
  variables `HTTP_*` via fastcgi).
- Symfony les accepte : `framework.yaml` →
  `trusted_proxies: '%env(TRUSTED_PROXIES)%'` +
  `trusted_headers: [x-forwarded-for, x-forwarded-host, x-forwarded-proto, x-forwarded-port]`,
  avec `TRUSTED_PROXIES=127.0.0.1,REMOTE_ADDR` (défaut `app/.env`, redéclaré dans
  `compose.yaml`) — `REMOTE_ADDR` = « faire confiance à l'upstream direct », soit
  nginx.

Si un jour l'edge change (autre proxy, CDN), vérifier que le nouvel intermédiaire
émet bien `X-Forwarded-Proto: https`, sinon adapter `TRUSTED_PROXIES`.

## 6. Comportement applicatif (rappel)

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
