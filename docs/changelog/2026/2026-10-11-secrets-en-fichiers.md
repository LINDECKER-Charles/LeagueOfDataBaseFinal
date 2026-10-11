---
date: 2026-10-11
type: devops
scope: infra
title: Les clés du site ne figurent plus dans la configuration visible des serveurs
summary: Paiements, e-mails, connexion Google et base de données reçoivent leurs clés en fichiers, chacun les siennes.
tags: [securite, deploiement]
---

## Ce qui change

Rien ne change pour les joueurs : dons, achats de crédits, e-mails, connexion avec Google et
comptes fonctionnent comme avant. En coulisses, les clés qui protègent ces services (clés de
paiement, mot de passe d'envoi des e-mails, accès à la base de données…) ne sont plus écrites
dans la configuration des services, que les outils d'administration du serveur affichent :
chaque service reçoit seulement les siennes, sous forme de fichiers qu'il est seul à lire.

## Pourquoi

Moins une clé est visible, moins elle risque de fuiter : c'est une protection de plus pour les
paiements et les comptes.

## Technique

- `tools/deploy/split-env.sh`, lancé par `_deploy.yml` sur l'hôte à la révision déployée,
  sépare `ENV_STAGING`/`ENV_PROD` : chaque secret dans `.deploy/secrets/<clé>` (444, dossier
  700, vide si absent), le reste dans `.env` avec `LODB_SECRETS_REVISION`. Il refuse, sans
  rien écrire, une valeur que Compose lirait autrement (`$` hors apostrophes, quote ouverte).
- `compose.deploy.yaml` : secrets Compose montés dans `/run/secrets` des seuls services qui
  les lisent ; plus aucun secret sous `environment:` ; chaîne de connexion sans mot de passe ;
  `POSTGRES_PASSWORD_FILE` ; label `league-of-data-base.secrets-revision` sur `api`, qui la
  fait recréer quand un secret change.
- API : `Hosting/Secrets/` lit `/run/secrets` (key-per-file, `LoDb:Hosting:SecretsDirectory`)
  au-dessus de toute autre source, un fichier vide valant un réglage absent ; complète
  `ConnectionStrings:LoDb` avec `LoDb:Database:Password` (API et `migrate`) ; log
  `hosting.secret_files.loaded` (noms seulement).
- Contrôles du déploiement : mot de passe de la base requis partout, clés Stripe (fichiers) et
  relais SMTP requis en prod. Guides : `configuration.md` § 4 et 5, `tools/deploy/README.md`.
