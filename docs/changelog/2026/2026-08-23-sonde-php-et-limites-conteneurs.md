---
date: 2026-08-23
type: devops
scope: infra
title: Une panne du moteur du site est maintenant detectee toute seule
summary: Le conteneur applicatif se declare en panne des que son moteur ne repond plus, et chaque service a desormais un plafond memoire surveille.
tags: [healthcheck, docker, supervision, mailer]
---

## Ce qui change

Le site vérifie en continu que son moteur PHP **répond réellement**, et pas seulement
qu'il est lancé. Si le moteur meurt, le conteneur bascule immédiatement en panne et
redémarre, au lieu de rester marqué « en bonne santé » en servant des erreurs.

Chaque service a également un plafond mémoire, ce qui permet à la supervision de
prévenir **avant** la saturation plutôt qu'après le crash.

## Pourquoi

L'ancienne sonde exécutait `php -r 'exit(0);'` : elle prouvait seulement que le binaire
PHP existait sur le disque. Un moteur mort passait donc inaperçu.

## Détails

- La boîte mail de test (Mailpit) ne démarre plus que sur un poste de développement.
- Les redémarrages de déploiement ne font plus clignoter les conteneurs en « panne »
  pendant leur phase de démarrage.

## Technique

- **Sonde php** : `cgi-fcgi -bind -connect 127.0.0.1:9000` sur `ping.path`, avec
  `grep -q pong` — le corps doit être vérifié, `cgi-fcgi` sortant aussi en 0 sur un
  « File not found » de FPM. La réponse vient d'un process **enfant**, donc un
  worker a réellement accepté et exécuté une requête. `RUN apk add --no-cache fcgi`
  dans l'étage `php_base` (36 Kio) : placé dans `app`, il tomberait après
  `USER www-data` et échouerait. `access.suppress_path[] = /ping` pour que la sonde
  ne réintroduise pas le bruit qu'on vient de supprimer.
- **`start_period`** sur les cinq healthchecks (php 30s, nginx 10s, go-fetcher 5s,
  go-api 15s, postgres 30s) : sans lui chaque échec de la phase de démarrage compte
  comme un échec réel, et les conteneurs passent transitoirement `unhealthy` à chaque
  déploiement.
- **Limites mémoire** dans `compose.deploy.yaml` uniquement, jamais `compose.yaml` (le
  fichier du dev, où `composer install` tourne en conteneur) : php 1g, minio 1g,
  go-fetcher 768m, postgres 512m, nginx/go-api 256m. Sans limite, cAdvisor publie
  `container_spec_memory_limit_bytes = 0` et la garde `> 0` des règles d'infra écarte
  deux alertes — elles étaient structurellement inertes. Pas de plafond `cpus` : trop
  bas il bride en silence.
- **Mailpit derrière `profiles: ["dev"]`** — un sink SMTP accepte et jette le courrier,
  donc un environnement servi qui y retombe perdrait silencieusement les e-mails de
  compte. En contrepartie `MAILER_DSN` devient obligatoire dans `compose.deploy.yaml`
  (`${MAILER_DSN:?…}`) et `COMPOSE_PROFILES=dev` est posé dans `.env` **et**
  `.env.example` — `.env` étant git-ignoré, sans quoi un nouveau clone perdrait Mailpit
  sans message.
