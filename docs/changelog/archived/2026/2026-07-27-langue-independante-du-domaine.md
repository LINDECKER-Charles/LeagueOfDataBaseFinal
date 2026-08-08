---
date: 2026-07-27
type: feat
scope: back
title: La langue ne dépend plus du domaine visité
summary: Le site s'affiche dans la langue que vous avez choisie, que vous arriviez par le .fr ou le .com, et une seule adresse fait référence pour les moteurs.
tags: [i18n, seo, langues]
---

## Ce qui change

Arriver sur `league-of-data-base.fr` n'impose plus le français, et arriver sur
le `.com` n'impose plus l'anglais. Seule compte la langue que vous avez
sélectionnée dans l'en-tête ; tant que vous n'en avez pas choisi, le site
s'affiche dans sa langue par défaut, quel que soit le domaine.

Les quatre adresses du site (avec ou sans `www`, `.fr` ou `.com`) affichent donc
exactement la même chose, et pointent toutes vers une adresse de référence unique.

## Pourquoi

Faire dépendre la langue du domaine créait deux comportements différents pour un
même site, sans que le visiteur l'ait demandé. Une fois cette règle retirée, les
quatre domaines servent un contenu identique : sans adresse de référence unique,
les moteurs de recherche les auraient traités comme quatre copies concurrentes du
site, chacune diluant le classement des autres.

## Détails

- Le choix de langue reste mémorisé comme avant (session et cookie de préférence).
- Les liens partagés continuent de fonctionner à l'identique sur les quatre domaines.

## Technique

`LocaleSubscriber` ne dérive plus la locale par défaut du TLD : elle vient de
`%kernel.default_locale%`, la sélection du visiteur restant la source de vérité.

Conséquence directe côté référencement : plus aucune locale n'a d'URL adressable,
donc **aucun `hreflang` n'est émis** — en annoncer un vers des adresses qui ne
servent pas cette langue serait pire que de n'en annoncer aucun. À la place,
`CanonicalHost` replie le préfixe `www.` **et** les domaines miroirs vers
`seo.canonical_host` (`config/packages/seo.yaml`). Le repli est opt-in par
environnement : sans hôte préféré configuré, chaque requête reste self-canonical,
donc dev, staging et previews ne pointent jamais vers la production.

Le `robots.txt` conserve volontairement **un seul groupe de règles** : les groupes
robots.txt ne s'héritent pas, et ajouter un bloc par agent IA aurait forcé à
dupliquer — puis maintenir en phase — toute la liste des `Disallow`.

Reste ouvert (hors périmètre) : la redirection 301 `www` → apex au niveau de l'edge
Caddy. Le canonical suffit à l'indexation, mais la redirection éviterait de servir
le contenu quatre fois.
