---
date: 2026-07-27
type: feat
scope: full-stack
title: Trois pages expliquent enfin ce qu'est le projet
summary: Une page À propos, une page Données et sources et une FAQ décrivent le site, ses sources et ses limites, dans les 21 langues.
tags: [contenu, seo, i18n, faq]
---

## Ce qui change

Le site explique désormais ce qu'il est. Trois pages s'ajoutent, accessibles
depuis le pied de page :

- **À propos** — ce qu'est League Of Data Base, à qui il s'adresse, ce qu'on
  peut y faire, et le nombre de champions, objets, runes et sorts publiés.
- **Données et sources** — d'où viennent les valeurs affichées (Data Dragon,
  Community Dragon), à quelle fréquence elles sont rafraîchies, ce qui est
  couvert patch par patch, et ce que le site ne prétend pas faire.
- **FAQ** — onze questions concrètes : gratuité, compte nécessaire ou non,
  anciens patchs, langues, API, hors ligne, lien avec Riot Games.

Les trois pages sont disponibles dans les 21 langues du site.

## Pourquoi

Jusqu'ici, aucune page ne disait en toutes lettres ce que fait le projet : un
visiteur — ou un moteur de recherche — devait le déduire des centaines de fiches
de données. Résultat, le site restait difficile à trouver quand on le décrivait
plutôt que de le nommer, et les questions les plus fréquentes n'avaient de
réponse nulle part.

## Détails

- La page Données affiche un instantané réel : patch courant, nombre d'entrées
  par section, nombre de langues et de patchs disponibles.
- Les limites sont assumées explicitement : pas de taux de victoire, pas de tier
  list, pas d'historique de parties — ces données ne viennent pas de Data Dragon.
- La mention Riot Games (projet non affilié) figure sur les pages Données et FAQ.

## Technique

`AboutController` (`/about`, `/about/data`, `/faq`) dérive d'`AbstractResourceController`.
Les Q/R vivent dans le catalogue `about` sous `faq.<id>.question` / `.answer` ; la
liste d'ids `AboutController::FAQ_ENTRIES` est le contrat, et sert **à la fois** le
rendu HTML et le nœud `FAQPage` — les réponses visibles et celles exposées aux
crawlers ne peuvent donc pas diverger.

Nouveau service `SiteInventory` → `InventorySnapshot` (patch + compteurs par
section, chaque section retombant à 0 en cas de panne upstream plutôt que de faire
tomber la page). Le snapshot alimente les pages À propos **et** `/llms.txt`, ce qui
évite de redéclarer les mêmes chiffres à deux endroits.

21 catalogues `about.<locale>.yaml`, plus les clés `about.*` ajoutées aux 21
catalogues `seo.*` (titres + meta descriptions). Layout partagé
`templates/about/layout.html.twig` (chrome + feuille de style prose scopée), sur le
modèle de `legal/layout.html.twig` — à ceci près que tout le corps passe par `|trans`.

Couverture : `EditorialPagesTest` (smoke des trois routes + du graphe structuré). Les
cas HTML se **skippent** quand les jeux de données Data Dragon sont absents — le
header partagé construit sa navigation depuis la liste des patchs, donc *toutes* les
pages du site échouent sans eux (la CI n'a ni MinIO ni go-fetcher). Un rouge ici est
donc toujours une vraie régression.
