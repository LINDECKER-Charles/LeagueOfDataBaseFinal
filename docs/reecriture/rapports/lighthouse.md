# Lighthouse : budgets du lot 3 (L3.13)

- **Date** : 2026-09-26.
- **Réécriture** : http://localhost:18080 (lodb-next).
- **Outil** : Lighthouse 13.5.0, HeadlessChrome/153.0.0.0.
- **Profil** : mobile par défaut de Lighthouse (écran 412 × 823, CPU ralenti 4 fois, 4G
  lente simulée : 150 ms de RTT, 1,6 Mbit/s), stockage vidé avant chaque passe.
- **Passes** : 3 par page ; chaque chiffre est la médiane des passes, les
  détails viennent de la passe de performance médiane.
- **Commande** : `node tools/next/lighthouse/run.mjs --stack lodb-next`.

Rapport généré : ne pas le modifier à la main, relancer `tools/next/lighthouse/run.mjs`.

## Budgets

Performance ≥ 90 ; Accessibilité ≥ 95 ; SEO ≥ 95 ; LCP ≤ 2500 ms ; CLS ≤ 0.1.

## Réécriture

| Page | Perf. | Access. | Bonnes pr. | SEO | FCP | LCP | TBT | CLS | Speed Index | Budgets |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|:---|
| [Accueil](http://localhost:18080/en/) | 87 | 100 | 100 | 100 | 3.0 s | 3.3 s | 80 ms | 0.007 | 3.0 s | **✗** Performance, LCP |
| [Liste des champions](http://localhost:18080/en/champions) | 85 | 98 | 100 | 100 | 3.0 s | 3.5 s | 37 ms | 0.000 | 3.0 s | **✗** Performance, LCP |
| [Champion (Annie)](http://localhost:18080/en/champions/Annie) | 73 | 100 | 100 | 100 | 3.2 s | 5.5 s | 48 ms | 0.008 | 3.2 s | **✗** Performance, LCP |
| [Objet (Infinity Edge)](http://localhost:18080/en/items/3031-infinity-edge) | 86 | 100 | 100 | 100 | 3.0 s | 3.4 s | 41 ms | 0.011 | 3.0 s | **✗** Performance, LCP |
| [À propos (prérendue)](http://localhost:18080/en/about) | 92 | 100 | 100 | 100 | 2.6 s | 2.7 s | 36 ms | 0.000 | 2.6 s | **✗** LCP |

**Verdict** : budgets manqués sur 5 page(s) sur 5 : Accueil, Liste des champions, Champion (Annie), Objet (Infinity Edge), À propos (prérendue).

## Détails de la réécriture

### Accueil

- Piste de performance `network-dependency-tree-insight` (Network dependency tree).
- Piste de performance `unused-javascript` (Reduce unused JavaScript) : Est savings of 48 KiB.

### Liste des champions

- accessibility en échec : `heading-order` (Heading elements are not in a sequentially-descending order).
- Piste de performance `network-dependency-tree-insight` (Network dependency tree).
- Piste de performance `interactive` (Time to Interactive) : 4.2 s.
- Piste de performance `unused-javascript` (Reduce unused JavaScript) : Est savings of 43 KiB.
- 2 ressource(s) texte servie(s) sans compression, 2 Kio : `/build/chunk-CGAFpfEj.js`, `/build/chunk-BHxMYKwX.js`.

### Champion (Annie)

- Piste de performance `cache-insight` (Use efficient cache lifetimes) : Est savings of 3,696 KiB.
- Piste de performance `image-delivery-insight` (Improve image delivery) : Est savings of 286 KiB.
- Piste de performance `network-dependency-tree-insight` (Network dependency tree).
- Piste de performance `interactive` (Time to Interactive) : 5.5 s.
- Piste de performance `unused-javascript` (Reduce unused JavaScript) : Est savings of 45 KiB.
- Piste de performance `total-byte-weight` (Avoid enormous network payloads) : Total size was 4,187 KiB.
- 1 ressource(s) texte servie(s) sans compression, 1 Kio : `/build/chunk-CGAFpfEj.js`.

### Objet (Infinity Edge)

- Piste de performance `image-delivery-insight` (Improve image delivery) : Est savings of 6 KiB.
- Piste de performance `network-dependency-tree-insight` (Network dependency tree).
- Piste de performance `unused-javascript` (Reduce unused JavaScript) : Est savings of 46 KiB.

### À propos (prérendue)

- Piste de performance `network-dependency-tree-insight` (Network dependency tree).
- Piste de performance `unused-javascript` (Reduce unused JavaScript) : Est savings of 48 KiB.
- 1 ressource(s) texte servie(s) sans compression, 1 Kio : `/api/catalog/16.19.1/en_US/items?page=1&size=1`.

## Prod, à titre indicatif

Mesurée sur https://league-of-data-base.com, mêmes réglages.

| Page | Perf. | Access. | Bonnes pr. | SEO | FCP | LCP | TBT | CLS | Speed Index |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| [Accueil](https://league-of-data-base.com/) | 100 | 100 | 100 | 100 | 1.0 s | 1.7 s | 0 ms | 0.000 | 1.0 s |
| [Liste des champions](https://league-of-data-base.com/champions) | 67 | 98 | 100 | 100 | 1.5 s | 4.0 s | 33 ms | 0.465 | 1.5 s |
| [Champion (Annie)](https://league-of-data-base.com/champion/Annie) | 97 | 100 | 100 | 100 | 1.0 s | 2.6 s | 0 ms | 0.000 | 1.0 s |
| [Objet (Infinity Edge)](https://league-of-data-base.com/object/3031) | 100 | 100 | 100 | 100 | 1.0 s | 1.7 s | 0 ms | 0.000 | 1.0 s |
| [À propos (prérendue)](https://league-of-data-base.com/about) | 100 | 100 | 100 | 100 | 1.0 s | 1.7 s | 0 ms | 0.000 | 1.0 s |

## Détails de la prod

### Accueil

- Piste de performance `cache-insight` (Use efficient cache lifetimes) : Est savings of 191 KiB.
- Piste de performance `image-delivery-insight` (Improve image delivery) : Est savings of 153 KiB.
- Piste de performance `network-dependency-tree-insight` (Network dependency tree).
- Piste de performance `render-blocking-insight` (Render-blocking requests).
- Piste de performance `bootup-time` (Reduce JavaScript execution time) : 3.9 s.
- Piste de performance `mainthread-work-breakdown` (Minimize main-thread work) : 4.7 s.
- 1 ressource(s) texte servie(s) sans compression, 2 Kio : `/manifest.webmanifest`.

### Liste des champions

- accessibility en échec : `heading-order` (Heading elements are not in a sequentially-descending order).
- Piste de performance `cache-insight` (Use efficient cache lifetimes) : Est savings of 867 KiB.
- Piste de performance `cls-culprits-insight` (Layout shift culprits).
- Piste de performance `forced-reflow-insight` (Forced reflow).
- Piste de performance `image-delivery-insight` (Improve image delivery) : Est savings of 458 KiB.
- Piste de performance `lcp-discovery-insight` (LCP request discovery).
- Piste de performance `modern-http-insight` (Modern HTTP).
- Piste de performance `network-dependency-tree-insight` (Network dependency tree).
- Piste de performance `render-blocking-insight` (Render-blocking requests).
- Piste de performance `interactive` (Time to Interactive) : 4.1 s.
- Piste de performance `layout-shifts` (Avoid large layout shifts) : 1 layout shift found.
- 1 ressource(s) texte servie(s) sans compression, 2 Kio : `/manifest.webmanifest`.

### Champion (Annie)

- Piste de performance `cache-insight` (Use efficient cache lifetimes) : Est savings of 3,700 KiB.
- Piste de performance `image-delivery-insight` (Improve image delivery) : Est savings of 311 KiB.
- Piste de performance `network-dependency-tree-insight` (Network dependency tree).
- Piste de performance `render-blocking-insight` (Render-blocking requests).
- Piste de performance `total-byte-weight` (Avoid enormous network payloads) : Total size was 3,902 KiB.
- 1 ressource(s) texte servie(s) sans compression, 2 Kio : `/manifest.webmanifest`.

### Objet (Infinity Edge)

- Piste de performance `cache-insight` (Use efficient cache lifetimes) : Est savings of 1 KiB.
- Piste de performance `image-delivery-insight` (Improve image delivery) : Est savings of 6 KiB.
- Piste de performance `network-dependency-tree-insight` (Network dependency tree).
- Piste de performance `render-blocking-insight` (Render-blocking requests).
- 1 ressource(s) texte servie(s) sans compression, 2 Kio : `/manifest.webmanifest`.

### À propos (prérendue)

- Piste de performance `network-dependency-tree-insight` (Network dependency tree).
- Piste de performance `render-blocking-insight` (Render-blocking requests).
- 1 ressource(s) texte servie(s) sans compression, 2 Kio : `/manifest.webmanifest`.
