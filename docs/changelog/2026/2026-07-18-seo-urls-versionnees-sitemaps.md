---
date: 2026-07-18
type: feat
scope: full-stack
title: Pages par patch indexables + sitemaps par version
summary: Chaque patch d'un champion, objet, rune ou sort a désormais sa propre page durable et une sitemap dédiée, tout en gardant le dernier patch sur l'URL principale.
tags: [seo, sitemap, versioning, routing]
---

## Ce qui change

On peut maintenant ouvrir la fiche d'un champion, d'un objet, d'une rune ou d'un
sort **telle qu'elle était à un patch précis**, à une adresse propre et stable du
type `…/16.13.1/champion/Aatrox`. En naviguant depuis une de ces pages, on **reste
sur ce patch** : les cartes, le fil d'Ariane, le menu et le pied de page pointent
tous vers la même version. Le sélecteur en haut affiche le patch consulté.

Le **dernier patch** garde toujours l'adresse courte et canonique (`…/champion/Aatrox`)
et reste la page mise en avant : c'est elle qui porte les données du patch en cours.

Côté moteurs de recherche, le plan du site liste désormais une **sitemap par
version** (plus une principale sur le dernier patch), pour que l'historique des
patchs soit explorable et indexable.

## Pourquoi

Jusqu'ici, une seule version (le dernier patch) était réellement indexable : tout
l'historique des patchs existait mais restait invisible pour la recherche. Rendre
chaque patch adressable et découvrable ouvre la longue traîne (« Aatrox patch
16.13 ») sans dupliquer la page principale.

## Détails

- Nouvelles URLs `…/{version}/…` pour les 4 listes et les 4 fiches.
- Le dernier patch en `/{version}/…` redirige (301) vers l'URL courte : une seule
  page indexable par (entité, patch).
- Titres différenciés par patch (« … (patch 16.13.1) ») pour éviter les doublons.
- Plan du site : `/sitemap.xml` devient l'index ; `/sitemaps/latest.xml` = principal,
  `/sitemaps/{version}.xml` = une par patch.

## Technique

- Routes versionnées ajoutées en second `#[Route]` sur chaque action ressource
  (`requirement` = `VersionManager::VERSION_PATTERN`) ; le corps des contrôleurs est
  inchangé — `PageContextResolver::selection()` lit la version depuis l'attribut de
  route (précédence route > query > session), langue découplée (query > session).
- `VersionedRouteRedirectSubscriber` (kernel.request, prio 30) : 301 du dernier
  patch versionné vers l'URL propre. Canonical inchangé (`getPathInfo()` → self).
- Liens internes centralisés dans `ResourceUrlGenerator` + fonction Twig
  `resource_path()` (propre si dernier patch, sinon `/{version}/…`, `?lang=` conservé) ;
  chrome (header/footer/bottom-nav) dérive la version de `app.request.attributes`.
- Loader SSE : `urls.ts` lit la version du segment de chemin ; `loaderSteps()` strippe
  le préfixe version ; `dd-latest` exposé pour router le switch dans le chemin.
- Sitemaps : `SitemapBuilder` (index + urlset) ; historiques en `Cache-Control:
  immutable` (données figées). XML non traduit (standard).
- i18n : clé `seo.versioned_suffix` ajoutée aux 21 catalogues.
