---
date: 2026-07-17
type: feat
scope: full-stack
title: Votre profil d'invocateur — favoris, carte publique et contrôle du compte
summary: Le profil prend vie — choisissez vos 4 favoris, partagez votre carte d'invocateur, restez maître de votre compte.
tags: [profil, favoris, comptes]
---

## Ce qui change

Votre page profil devient une vraie « chambre de l'invocateur » : identité à
gauche, et à droite quatre emplacements à sertir — votre champion, votre objet,
votre rune et votre sort d'invocateur favoris. Vous pouvez aussi rendre votre
profil public et partager votre carte d'invocateur à l'adresse
`/u/votre-pseudo`.

## Détails

- Quatre emplacements de favoris : un clic ouvre un sélecteur avec recherche
  instantanée (insensible aux accents et à la casse) ; les runes y sont
  présentées par arbre, perks incluses — l'arbre entier peut être un favori.
- Profil privé par défaut. Une case à cocher le rend public : votre carte
  affiche alors pseudo, ancienneté, favoris et builds publiés — jamais votre
  e-mail (masqué même pour vous, façon `c***@exemple.fr`).
- Un favori choisi sur un ancien patch peut ne plus exister sur le patch
  affiché : l'emplacement l'indique sobrement au lieu de casser la page, et
  vous êtes prévenu si un enregistrement doit le retirer.
- Zone dangereuse en bas de profil : suppression définitive du compte,
  confirmée par votre mot de passe — vos builds partent avec (droit à
  l'effacement annoncé par nos pages légales).
- Profil introuvable ou privé : même page 404 pour les deux — impossible de
  deviner si un pseudo possède un compte.

## Technique

- API pickers `GET /api/picker/{champions,items,runes,summoners}` :
  projection pure par type (`App\Service\Picker\*OptionsProjector` +
  façade `PickerCatalog`), filtre serveur objets (purchasable, SR, non cachés,
  non liés champion, `from` dédupliqué), `shortDesc` des runes strippé
  serveur. `Cache-Control: public, max-age=3600` quand `?version=&lang=`
  valides explicites (header repris à `AbstractSessionListener`), sinon
  `private, max-age=0` ; erreur upstream → 503 JSON.
- Résolution des favoris présence-based (dataset brut) et version-scopée ;
  `FavoriteSelectionSanitizer` (pur, testé) nul-ifie les ids inconnus avec
  warning par slot ; panne data pendant la sauvegarde → refus explicite
  plutôt qu'effacement silencieux.
- `/u/{username}` : lookup insensible à la casse
  (`UserRepository::findOneByUsernameInsensitive`, additif), builds publics
  via `BuildRepository::findPublicByOwner` + portraits via le catalogue.
- Îlot `FavoritePicker` (remplace le stub) : `usePickerCatalog` (fetch
  paresseux par type, retry) + module pur `filterOptions` (accents/groupes,
  spec vitest) ; `<dialog>` `.filter-sheet` réutilisé, centré ≥ md ; les ids
  stockés non résolus round-trippent via les hidden inputs (le serveur
  décide). Fallback no-JS server-rendered (sockets statiques + hidden inputs).
- Suppression de compte : vérif `UserPasswordHasherInterface`, remove +
  cascade FK, `TokenStorage->setToken(null)` + invalidation session avant le
  flash d'adieu.
- Signature UI : socket vide au losange hex creux qui s'illumine cyan au
  survol (`--ease-hextech`, neutralisé `prefers-reduced-motion`) — styles
  dans `profile.css` uniquement.
