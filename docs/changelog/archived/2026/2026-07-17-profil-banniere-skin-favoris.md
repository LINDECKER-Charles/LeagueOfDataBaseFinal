---
date: 2026-07-17
type: feat
scope: full-stack
title: Bannière de skin et favoris décoratifs sur le profil
summary: Choisissez un skin en bannière de fond de profil, mettez vos favoris en vitrine, et prévisualisez votre carte publique.
tags: [profil, skins, favoris, personnalisation]
---

## Ce qui change

Votre profil devient une vitrine. Choisissez le **skin d'un champion** comme
bannière de fond : sélectionnez d'abord le champion, puis l'un de ses skins, et
son splash art habille tout le haut de votre profil. Vos favoris — champion,
objet, rune et sort d'invocateur — s'affichent désormais en **emblèmes
décoratifs** posés sur cette bannière.

Un nouveau bouton **« Aperçu du profil public »** vous montre votre carte
exactement telle que les autres la voient — même quand votre profil est encore
privé.

## Pourquoi

Le profil affichait déjà vos favoris, mais de façon utilitaire. Cette refonte en
fait un espace d'expression : votre main, votre skin préféré, votre style de jeu,
en un coup d'œil — et de quoi vérifier le rendu avant de rendre le profil public.

## Détails

- Bannière de skin : n'importe quel skin de n'importe quel champion.
- Favoris champion / objet / rune / sort mis en scène en emblèmes sur la bannière.
- Bouton d'aperçu de la carte publique, accessible même en profil privé.
- La carte publique (`/u/pseudo`) et l'aperçu partagent exactement le même rendu.

## Technique

- `users.favorite_skin_id` (`"{championId}_{skinNum}"`, ex. `Ahri_7`) = stem du
  fichier splash DDragon → l'URL de bannière se dérive sans lookup data ; art
  hotlinkée depuis le CDN (comme les splashs de la fiche champion), jamais
  ingérée MinIO. Validation au save = format + longueur (auto-valide via
  `onerror`), donc résistante à une panne du data layer.
- `ChampionSkins` : options de skins d'un champion (picker) + `resolveBanner`
  (art dérivée, nom best-effort via `getDetail`). `PublicProfileView` : modèle de
  rendu partagé entre `/u/{username}` et `/profile/preview` (aperçu = la page, pas
  une approximation).
- Endpoint `GET /api/picker/skins?champion=` ; îlot Vue `skin-banner-picker`
  (flux champion → skin dans le `<dialog>` partagé, orchestration en composable
  `useSkinCatalog`).
