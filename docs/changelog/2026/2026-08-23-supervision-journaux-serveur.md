---
date: 2026-08-23
type: devops
scope: infra
title: Les incidents du site remontent enfin dans la supervision
summary: Le serveur envoie désormais ses alertes vers la supervision au lieu de les jeter, et son journal ne peut plus saturer le disque.
tags: [observabilite, logs, docker, nginx]
---

## Ce qui change

Quand quelque chose casse (données Riot injoignables, page qui ne se charge pas), le
site le signale maintenant **immédiatement** à la supervision. Avant, ces signaux
étaient conservés en mémoire puis effacés à la fin de la requête : une panne pouvait
durer sans laisser la moindre trace.

Aucun changement visible sur le site lui-même : c'est de l'outillage de surveillance.

## Pourquoi

Une panne qu'on ne voit pas est une panne qu'on corrige tard, et seulement après qu'un
joueur l'a signalée.

## Technique

- **Monolog** : le `fingers_crossed` sans `passthru_level` du bloc `when@prod` jetait le
  buffer à la fin de chaque requête sans erreur — 14 des 17 appels existants
  n'atteignaient jamais `stderr`. Remplacé par deux flux always-on (`business` dès INFO
  sur les canaux métier, `stderr` dès NOTICE pour le reste) plus un `error_context`
  filtré `debug..info`, si bien que les niveaux sont partitionnés et qu'aucun
  enregistrement n'est écrit deux fois.
- **`framework.exceptions`** (obligatoire avec le handler always-on) : 404/405 rétrogradés
  en `info`, 429 des rate-limiters en `notice`. Sans ça chaque sonde de bot écrivait une
  ligne `ERROR` conservée 90 jours.
- **`php.ini`** : l'image n'activait aucun php.ini principal (`display_errors = 1`,
  `log_errors = 0`) — une erreur moteur partait dans le corps de la réponse HTTP et
  nulle part ailleurs. Filet posé (`display_errors = Off`, `error_log = /proc/self/fd/2`,
  `zend.exception_ignore_args = On` pour ne pas fuiter un DSN dans une trace).
- **`php-fpm.conf`** : `access.log = /dev/null` (le stdout du conteneur était à 100 %
  l'access-log FPM, ~2 350 lignes sans aucune ligne applicative), `log_limit = 65536`
  pour ne plus couper un enregistrement JSON en fragments, et un `slowlog` à 5 s.
- **nginx** : `access_log off` — intégralement redondant avec le journal de l'edge, et de
  qualité inférieure (`$remote_addr` = l'IP du conteneur Caddy). C'était aussi le vecteur
  qui laissait n'importe quel visiteur piloter le champ `level` de la supervision en
  demandant une URL contenant le mot « error ». Contrepartie assumée : plus de trace de
  requête en développement local.
- **compose** : ancre `x-logging` (json-file, 10 Mio × 3) sur les huit services, y compris
  `minio-init` dont la boucle d'attente sans timeout est le producteur de logs le plus
  susceptible de s'emballer.
