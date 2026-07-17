---
date: 2026-07-17
type: feat
scope: full-stack
title: Profil vivant — bannière de skin, favoris flottants et sauvegarde auto
summary: Un skin en fond de profil, vos favoris en éléments décoratifs animés, et tout s'enregistre tout seul.
tags: [profil, skins, favoris, personnalisation, ux]
---

## Ce qui change

Votre profil devient une vitrine vivante. Choisissez le **skin d'un champion**
comme décor de fond (sélectionnez le champion, puis le skin — les chromas sont
masqués, seuls les vrais skins sont proposés). À défaut de skin, votre **champion
favori** habille le fond.

Vos autres favoris — objet, rune, sort d'invocateur (et le champion quand un skin
occupe déjà le fond) — **flottent** au-dessus du décor avec une légère animation
d'idle, comme des éclats Hextech en suspension.

Plus besoin de cliquer sur « Enregistrer » : **chaque changement se sauvegarde
tout seul**. Le réglage de visibilité passe d'une case à cocher fade à une vraie
carte avec interrupteur, état public/privé clair et lien direct vers votre page.

Un bouton **« Aperçu du profil public »** montre votre carte telle que les autres
la voient, même en profil privé.

## Pourquoi

Le profil listait les favoris de façon utilitaire et exigeait une sauvegarde
manuelle. Cette refonte en fait un espace d'expression fluide : on personnalise,
ça s'enregistre, on prévisualise — sans friction.

## Détails

- Bannière de skin indépendante (n'importe quel skin de n'importe quel champion),
  chromas exclus de la sélection.
- **Profil perso** : le champion favori habille discrètement le fond de la page,
  et les favoris flottent en petite constellation dans l'en-tête.
- **Carte publique** : skin favori en fond (champion en secours), favoris en
  orbes flottants animés (idle), respectant « animations réduites ».
- Zone de sélection des favoris resserrée (skin + 4 emplacements plus compacts).
- Sauvegarde automatique des favoris, du skin et de la visibilité.
- Carte de visibilité repensée : interrupteur, état, URL publique.
- Aperçu de la carte publique, accessible même en profil privé.

## Technique

- `users.favorite_skin_id` (`"{championId}_{skinNum}"`, ex. `Ahri_7`) = stem du
  splash DDragon → URL de bannière dérivée sans lookup ; art hotlinkée du CDN.
- `ChampionSkins` : options de skins (chromas filtrés via `getChromas` +
  `withoutChromaSkins`), `resolveBanner`, `heroBackground` (skin → champion →
  gradient). `PublicProfileView` : modèle partagé `/u/{username}` ↔
  `/profile/preview`.
- Auto-save : `POST /profile` renvoie du JSON en XHR (redirect + flash conservés
  en no-JS). Îlots Vue émettant `profile:changed` ; enhancement `profileForm.ts`
  (debounce + statut + sync visibilité), hors îlot pour survivre au montage.
- Endpoint `GET /api/picker/skins?champion=` ; îlot `skin-banner-picker`
  (champion → skin dans le `<dialog>` partagé).
