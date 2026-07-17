---
date: 2026-07-17
type: feat
scope: full-stack
title: Créez votre compte d'invocateur et connectez-vous
summary: Inscription et connexion arrivent sur le site, dans l'écrin Hextech.
tags: [comptes, connexion, inscription]
---

## Ce qui change

Vous pouvez désormais créer votre compte d'invocateur et vous connecter au site.
Deux nouvelles pages au style « portail d'invocation » — carte dorée centrée,
anneau arcanique en toile de fond — accueillent l'inscription et la connexion.

## Détails

- Inscription avec e-mail, nom d'invocateur et mot de passe (8 caractères minimum) ;
  vous êtes connecté immédiatement après la création du compte.
- Connexion avec l'e-mail **ou** le nom d'invocateur, sans se soucier des
  majuscules, avec option « rester connecté » pendant 30 jours.
- Les tentatives de connexion répétées sont freinées pour protéger votre compte.
- Déconnexion en un clic ; votre page profil s'étoffera très bientôt (favoris,
  builds à partager).

## Technique

- Entités `User` / `Build` + repositories (login email OU username via
  `UserLoaderInterface`, unicité insensible à la casse alignée sur les index
  fonctionnels ; jetons de partage de build déjà prévus).
- Firewall `main` : form_login CSRF (stateless), remember_me 30 j, logout POST,
  login_throttling 5 essais / 15 min (symfony/rate-limiter). Firewall admin intact.
- Pages Twig `security/login` + `register` (form Symfony rendu à la main),
  utilitaire `hx-input` + token `--color-danger` + signature `auth.css` ajoutés au
  design system ; formulaires en `data-turbo="false"`.
- Stubs d'îlots Vue `favorite-picker`, `build-editor`, `copy-link` enregistrés pour
  les prochains lots.
