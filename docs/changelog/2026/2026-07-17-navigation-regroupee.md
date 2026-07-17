---
date: 2026-07-17
type: ui
scope: front
title: Navigation regroupée — Encyclopédie, compte et don dans la barre
summary: Les sections de données passent sous un menu « Encyclopédie », le compte et le don s'atteignent depuis la barre sur tous les écrans, et les pages légales sont liées au pied de page.
tags: [navigation, header, footer, compte, don]
---

## Ce qui change

La barre du haut fait de la place : Champions, Objets, Runes et Sorts
d'invocateur se rangent dans un menu déroulant « Encyclopédie » à côté de
l'Accueil. À droite, un bouton « Faire un don » et un menu « Compte » sont
désormais accessibles sur tous les écrans — connecté, le menu affiche votre
pseudo et mène à votre profil, vos builds et à la déconnexion ; sinon il
propose connexion et création de compte. Le pied de page gagne une colonne
« Légal » vers les mentions, la confidentialité, les CGU et les cookies.

## Détails

- Menu « Encyclopédie » : les quatre sections de données, avec la section en
  cours mise en évidence ; sur mobile, la barre du bas reste le raccourci.
- Menu « Compte » : icône seule sur mobile, pseudo (tronqué) sur grand écran ;
  déconnexion en un clic depuis le menu.
- Bouton don : gemme-cœur compacte dans la barre, libellé visible sur desktop.
- Pied de page : colonne « Légal » (4 pages), liens « Faire un don » et
  « Mon profil » (si connecté) dans la navigation, mention légale Riot Games
  en bas de page ; le lien « Mentions légales » pointe enfin la vraie page.
- Hors ligne : les pages privées (connexion, profil, builds, don) ne sont plus
  jamais servies depuis le cache de l'application.

## Technique

- Menus 100 % no-JS : même patron `<details class="switcher">` que le
  sélecteur patch/langue (variantes CSS `switcher--nav` / `switcher__panel--menu`
  dans `nav.css`) ; fermeture au clic extérieur déjà couverte par `enhance.ts`.
- Logout : POST CSRF (token id `logout`) `data-turbo="false"` dans le menu.
- `sw.js` : bypass étendu aux préfixes `/login`, `/register`, `/logout`,
  `/profile`, `/u/`, `/builds`, `/b/`, `/donate`, `/webhooks` (constante
  `BYPASS_PATH_PREFIXES`) — `/b/` ne matche pas `/build/` (assets Vite,
  toujours cache-first) ; `VERSION` bump `lodb-v1` → `lodb-v2`.
- `profile/index.html.twig` : le lien « Gérer mes builds » passe du href
  littéral `/builds` à `path('app_builds')`.
