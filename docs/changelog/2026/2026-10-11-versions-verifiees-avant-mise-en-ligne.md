---
date: 2026-10-11
type: devops
scope: infra
title: Chaque version du site est vérifiée avant sa mise en ligne
summary: Les composants du site sont figés et analysés contre les failles connues, et la version mise en ligne est exactement celle testée en préproduction.
tags: [securite, deploiement]
---

## Ce qui change

Rien ne change à l'écran. En coulisses, chaque nouvelle version du site est construite à partir
de composants figés, puis analysée contre les failles de sécurité connues avant d'aller plus
loin. La version mise en ligne est ensuite exactement celle qui a tourné sans erreur en
préproduction : une version qui embarque une faille déjà corrigée ailleurs, ou qui n'a pas
passé ses tests, n'atteint plus le site.

## Pourquoi

Le site s'appuie sur de nombreux composants tiers. Les vérifier à chaque version, et garantir
que ce qui est en ligne est bien ce qui a été testé, protège la disponibilité du site et les
comptes des joueurs.

## Technique

- Dockerfiles de `docker/` : bases et frontend `# syntax` épinglés par digest
  (`image:tag@sha256:…`), contrôlés par `tools/ci/check-pinned-images.mjs` (job
  `supply-chain`) ; Dependabot `docker` étendu à `docker/android-build`, mises à jour groupées.
- Étages `runtime` de nginx et web-ssr reconstruits sans cache par la CI : correctifs de la
  distribution à chaque build (`apk upgrade`, `apt-get upgrade`) ; web-ssr sans npm, corepack
  ni yarn.
- `_build.yml` : `test` résolu en un commit ; images poussées sous ce SHA avec SBOM et
  provenance (mode max) ; Trivy (épinglé par digest, sans identifiant du registre) bloque sur
  les vulnérabilités HIGH et CRITICAL corrigées en amont (`.trivyignore` daté si besoin) ;
  `:staging` posé ensuite sur les digests scannés (`tools/ci/tag-image.sh`).
- Promotion : `ci.yml` pose `:staging-validated` après un déploiement de staging réussi ;
  `_promote.yml` (`tools/ci/promote-images.sh`) pose `:prod` et `:latest` sur ces digests,
  et refuse si `:staging` a bougé, si deux builds se mélangent ou si la révision n'a pas le
  code de `main`.
- `npm audit --omit=dev` du front (lockfile seul), bloquant dès `high`, exceptions datées
  (90 jours au plus) dans `tools/ci/npm-audit-allowlist.json` ; `source-map-js` monté en 1.2.2.
- Guides : `tools/ci/README.md`, `docs/guides/github-actions-secrets.md`.
