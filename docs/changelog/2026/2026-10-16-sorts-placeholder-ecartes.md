---
date: 2026-10-16
type: fix
scope: back
title: Les faux sorts d'invocateur « Placeholder » disparaissent de l'encyclopédie
summary: Les deux emplacements techniques de Riot (« Placeholder » et « Placeholder and Attack-Smite ») ne sont plus listés, cherchables ni consultables, dans toutes les langues et toutes les versions.
tags: [seo, catalogue]
---

## Ce qui change

La liste des sorts d'invocateur ne montre plus que de vrais sorts : les deux entrées
« Placeholder » et « Placeholder and Attack-Smite » ont disparu de la liste, de la recherche
et du plan du site, et leur adresse répond désormais « introuvable », comme un objet
technique. Le nombre de sorts annoncé baisse de deux en conséquence.

## Pourquoi

Ces deux entrées sont des emplacements que le jeu remplit avec l'ultime d'un autre
champion : ce ne sont pas des sorts qu'un joueur peut choisir, et elles apparaissaient
pourtant comme des fiches à part entière dans les moteurs de recherche.

## Technique

- Règle unique `SummonerDebris` (domaine) : l'identifiant ou le nom en_US de la même version
  contient « Placeholder » (Data Dragon traduit le nom dans certaines langues). Appliquée à la
  lecture, comme pour les objets : aucune ré-ingestion.
- `CatalogSnapshot.ListedSummoners` / `IsListed(SummonerSpell)` alimentent la liste, ses
  facettes, le sélecteur, la recherche, le sitemap, `llms.txt`, les tendances, la fiche (404),
  la page d'un ancien patch (`PageOf`), les favoris, la résolution des anciennes adresses et
  le suivi d'audience.
- La résolution des anciennes adresses écarte aussi les objets « debris » : `/object/7050`
  (Gangplank Placeholder) répondait une 301 vers une fiche introuvable, il répond 404.
