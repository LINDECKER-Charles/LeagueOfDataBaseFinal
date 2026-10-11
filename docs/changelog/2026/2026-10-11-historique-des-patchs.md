---
date: 2026-10-11
type: feat
scope: full-stack
title: Une page « Historique des patchs » ouvre chaque ancien patch en un clic
summary: Tous les patchs publiés par Riot sont listés sur une seule page, chacun avec ses champions, objets, runes et sorts d'invocateur.
tags: [navigation, anciens-patchs, referencement]
---

## Ce qui change

Le pied de page mène à une nouvelle page, « Historique des patchs ». Elle liste tous les
patchs que Riot publie dans ses données officielles, du plus récent au plus ancien, regroupés
par numéro (16.x, 15.x…). Chaque patch ouvre directement ses champions, ses objets, ses runes
ou ses sorts d'invocateur tels qu'ils étaient à l'époque, sans passer par le sélecteur de
version. La page existe dans les 21 langues du site.

Chaque fiche (champion, objet, page de runes, sort d'invocateur) se termine aussi par un bloc
« Dans les patchs précédents » : un clic ouvre la même fiche dans l'un des six patchs
précédents.

## Pourquoi

Jusqu'ici, un ancien patch ne s'atteignait que par le sélecteur de version. Une page de liens
est plus directe pour les joueurs, et les moteurs de recherche, qui ne manipulent pas les
sélecteurs, trouvent enfin chaque ancien patch en suivant les liens du site.

## Technique

- Route `/{locale}/patches` (`features/patches/`), rendue par le serveur à partir de
  `/api/meta` seul : toutes les versions que Data Dragon liste (`versions`, pas seulement
  celles déjà prêtes), groupées par premier segment. Les listes du dernier patch pointent vers
  les URL courtes, les autres vers l'URL épinglée : aucun lien ne répond par une redirection.
- Aucun catalogue n'est lu pour rendre la page : son coût ne dépend pas du nombre de versions.
  Balisage allégé (sans encapsulation de styles), quatre liens `UrlTree` par version.
- Bloc `lodb-previous-patches` (`features/catalogue/shared/codex/history/`) sous les quatre
  fiches : la même entrée dans les six versions listées avant celle de la page, par
  `injectCatalogueLink` (UrlTree, `?lang=` conservé), à partir du `/api/meta` déjà lu par le
  résolveur. Aucun catalogue de ces versions n'est chargé pour rendre la fiche.
- Index des sitemaps (`/sitemap.xml`) borné au dernier patch et aux 12 versions listées
  ensuite (`LoDb__Seo__SitemapVersionWindow`, ADR 0005 amendée). Les sitemaps plus anciens
  restent servis et immuables, leurs pages canoniques et indexables ; le sitemap principal de
  chaque locale liste la page d'historique.
- Lien `footer.navigation.patches` dans le pied de page, titre `detail.history.title` du
  bloc, textes de la page dans `public/i18n/patches/` : traduits dans les 21 locales. Tête
  indexable de la page (canonique, `hreflang`, fil d'Ariane) par `Seo.apply`.
