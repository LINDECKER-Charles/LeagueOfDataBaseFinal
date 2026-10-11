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
- Lien `footer.navigation.patches` dans le pied de page ; textes de la page dans
  `public/i18n/patches/` pour les 21 locales ; tête indexable (canonique, `hreflang`, fil
  d'Ariane) par `Seo.apply`.
