---
date: 2026-07-17
type: feat
scope: full-stack
title: Connectez-vous avec votre compte Google
summary: Un bouton « Continuer avec Google » sur la connexion et l'inscription crée ou rattache votre compte en un clic.
tags: [comptes, connexion, google, oauth]
---

## Ce qui change

Les pages de connexion et d'inscription proposent désormais « Continuer avec
Google » sous un séparateur « ou ». Premier passage : votre compte est créé
instantanément (pseudo d'invocateur généré, modifiable ensuite) ou rattaché à
votre compte existant s'il utilise le même e-mail. Les comptes créés via Google
n'ont pas de mot de passe : une section dédiée du profil permet d'en définir un
(mêmes exigences CNIL, même checklist) pour se connecter aussi par e-mail.

## Détails

- Bouton sobre avec le monogramme G, sans ressource externe.
- Rattachement automatique par e-mail uniquement si Google certifie l'adresse
  vérifiée — sinon un compte distinct est créé (protection anti-usurpation).
- Sans configuration serveur, le bouton se dégrade proprement : retour à la
  connexion avec un message « connexion Google non configurée ».
- La suppression de compte reste possible pour les comptes Google sans mot de
  passe (confirmation par mot de passe remplacée par la protection CSRF, pour
  garantir le droit à l'effacement).

## Technique

- `knpuniversity/oauth2-client-bundle` v2.20 + `league/oauth2-google` v5
  (bundle enregistré à la main : recipes contrib refusées). Config
  `knpu_oauth2_client.yaml`, secrets `OAUTH_GOOGLE_CLIENT_ID/SECRET` (blocs
  `app/.env` + `compose.yaml` php.environment, vides par défaut).
- `GoogleConnectController` (`/connect/google` → consentement, scopes
  openid/profile/email ; `/connect/google/check` fallback si non configuré) et
  `GoogleAuthenticator` (`OAuth2Authenticator`, `SelfValidatingPassport`)
  déclaré en `custom_authenticators` du firewall `main` avec
  `entry_point: form_login` conservé.
- Provisionnement dans `GoogleAccountProvisioner` : (1) lookup `googleId`
  (claim `sub`, colonne VARCHAR(30) UNIQUE) ; (2) rattachement par e-mail si
  `email_verified` ; (3) création — username dérivé du prénom / local-part
  d'e-mail par `UsernameAllocator` (translittération AsciiSlugger, pattern
  `USERNAME_PATTERN`, suffixe séquentiel puis aléatoire, testé unitairement).
- `users.password` devient NULLable ; `form_login` échoue proprement (pas de
  500) sur un compte sans mot de passe. Route `app_profile_password`
  (SetPasswordFormType, contraintes CnilPassword partagées) visible seulement
  tant que `password IS NULL` ; suppression de compte adaptée.
- Étapes manuelles Google Cloud Console (consentement, publication sans revue,
  client Web, redirect URIs exactes, secrets, piège trusted_proxies) :
  `docs/oauth-google-setup.md`.
