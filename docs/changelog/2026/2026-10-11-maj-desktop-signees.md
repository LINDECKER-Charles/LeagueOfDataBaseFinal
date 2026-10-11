---
date: 2026-10-11
type: feat
scope: apps
title: L'app desktop n'installe plus que les mises à jour signées par nous
summary: Avant d'installer une mise à jour, l'app desktop vérifie notre signature ; une version qui n'en porte pas est ignorée.
tags: [securite, desktop, mises-a-jour]
---

## Ce qui change

L'app desktop se met toujours à jour seule, en arrière-plan. Avant de télécharger une nouvelle
version, elle vérifie désormais que la liste des versions disponibles porte notre signature,
puis que chaque fichier téléchargé est exactement celui que cette liste annonce. Une version
dont la signature manque ou ne correspond pas est ignorée : l'app reste sur la version qu'elle
a, sans rien vous demander.

## Pourquoi

Une mise à jour s'installe sans intervention de votre part. Il faut donc qu'elle ne puisse
venir que de nous : seule notre signature en fait foi, quel que soit l'endroit d'où l'app la
télécharge.

## Technique

- Signature Ed25519 de chaque `releases.<canal>.json` (`.sig` à côté), sur un message qui lie
  la signature au nom du flux, donc à son RID et à son canal. Clés publiques compilées dans
  l'app (`UpdateFeeds.FeedPublicKeys`, une seule hors rotation), clé privée dans le secret
  `LODB_DESKTOP_UPDATE_PRIVATE_KEY`.
- L'app lit GitHub Releases et le dossier local de la porte par `SignedGithubSource` et
  `SignedFolderSource` : un flux sans signature valide compte comme absent
  (`desktop.update.feed_refused`), un paquet dont la taille ou le SHA-256 diffère du flux signé
  est refusé (`desktop.update.package_refused`). Le rapport `--smoke` affiche la clé de
  confiance (`feed-key: ed25519 …`).
- Release : nouveau job `sign` (Node seul, sans npm ni build), porte qui vérifie aussi le refus
  d'un flux altéré, publication qui revérifie le flux signé après l'upload. Guide :
  `docs/guides/release-desktop.md` § Flux signés.
