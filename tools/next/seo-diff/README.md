# Diff SEO : la prod face à la réécriture (L3.13)

Compare, pour une quarantaine d'URLs de la prod, ce que lisent les robots : titre,
description, canonique, types et champs du JSON-LD. Node seul, sans dépendance.
Rapport : [`docs/reecriture/rapports/diff-seo.md`](../../../docs/reecriture/rapports/diff-seo.md).

```bash
# Contre la stack lodb-next de la racine (ou un emplacement : --next http://localhost:18180)
node tools/next/seo-diff/diff.mjs --stack lodb-next
# Tests de l'outil
node --test 'tools/next/seo-diff/test/*.test.mjs'
```

Options : `--next` (défaut `LODB_E2E_BASE_URL`, puis `http://localhost:18080`), `--prod`
(défaut `https://league-of-data-base.com`), `--stack` (nom affiché dans le rapport),
`--out` (défaut le rapport ci-dessus), `--json <fichier>` (résultats bruts), `--pause-ms`
(pause entre deux requêtes à la prod, défaut 250).

Code de sortie : 0 si chaque écart est expliqué, 1 s'il reste un écart non expliqué, 2 si
un site ne répond pas ou si la stack n'a ingéré aucune version.

## Déroulé

1. `/api/meta` de la stack donne la dernière version (clé `latest` des pages).
2. Chaque URL de `lib/sample.mjs` est lue sur la prod en GET seul, sans cookie, avec
   `Accept-Language: en`, une requête à la fois (`lib/collect.mjs`).
3. La même URL, sous sa forme héritée, est demandée à la stack sans suivre la redirection :
   la 301 de L3.12 désigne la page comparée, lue ensuite.
4. `lib/head.mjs` lit le `<head>` des deux réponses ; `lib/page-key.mjs` réduit chaque URL
   du site, des deux grammaires, à une clé (`detail:items:3031@latest`) ; `lib/json-ld.mjs`
   aplatit le JSON-LD en `chemin → valeur` après réécriture des URL en clés.
5. `lib/compare.mjs` compare, `lib/classify.mjs` explique chaque écart par une règle de
   `lib/rules.mjs`, et `lib/report.mjs` écrit le rapport.

## Règles

Une règle n'efface jamais un écart : le rapport liste chaque écart avec sa règle. Trois
sortes : `decision` (voulu par un ADR ou le plan), `correction` (défaut de la prod que la
réécriture n'a plus) et `defect` (régression de la réécriture, consignée pour le jalon).
Un écart nouveau se traite en corrigeant la page ou en ajoutant une règle argumentée, puis
en relançant l'outil : le rapport n'est jamais modifié à la main.
