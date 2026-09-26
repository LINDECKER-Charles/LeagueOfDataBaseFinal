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
| [Accueil](http://localhost:18080/en/) | 69 | 100 | 100 | 100 | 4.8 s | 5.1 s | 80 ms | 0.007 | 4.8 s | **✗** Performance, LCP |
| [Liste des champions](http://localhost:18080/en/champions) | 68 | 98 | 100 | 100 | 4.9 s | 5.4 s | 34 ms | 0.001 | 4.9 s | **✗** Performance, LCP |
| [Champion (Annie)](http://localhost:18080/en/champions/Annie) | 63 | 100 | 100 | 100 | 5.0 s | 7.4 s | 31 ms | 0.009 | 5.0 s | **✗** Performance, LCP |
| [Objet (Infinity Edge)](http://localhost:18080/en/items/3031-infinity-edge) | 68 | 100 | 100 | 100 | 4.9 s | 5.3 s | 39 ms | 0.011 | 4.9 s | **✗** Performance, LCP |
| [À propos (prérendue)](http://localhost:18080/en/about) | 68 | 100 | 100 | 100 | 5.1 s | 5.2 s | 31 ms | 0.001 | 5.1 s | **✗** Performance, LCP |

**Verdict** : budgets manqués sur 5 page(s) sur 5 : Accueil, Liste des champions, Champion (Annie), Objet (Infinity Edge), À propos (prérendue).

## Détails de la réécriture

### Accueil

- Piste de performance `network-dependency-tree-insight` (Network dependency tree).
- Piste de performance `interactive` (Time to Interactive) : 5.9 s.
- Piste de performance `unused-javascript` (Reduce unused JavaScript) : Est savings of 129 KiB.
- 10 ressource(s) texte servie(s) sans compression, 500 Kio : `/build/chunk-BOwvoBif.js`, `/build/chunk-D1YYfR6G.js`, `/build/chunk-bWyhc9VP.js`, `/build/chunk-B26mR3AF.js`, `/build/chunk-D9X3rG5m.js`, `/build/chunk-tOneJ2Jc.js`, `/build/main-NJFDD5X2.js`, `/build/chunk-j8wGpWlr.js`, `/build/chunk-Cgkukc-o.js`, `/manifest.webmanifest`.

### Liste des champions

- accessibility en échec : `heading-order` (Heading elements are not in a sequentially-descending order).
- Piste de performance `network-dependency-tree-insight` (Network dependency tree).
- Piste de performance `interactive` (Time to Interactive) : 6.4 s.
- Piste de performance `unused-javascript` (Reduce unused JavaScript) : Est savings of 165 KiB.
- 19 ressource(s) texte servie(s) sans compression, 642 Kio : `/build/chunk-BOwvoBif.js`, `/build/chunk-D1YYfR6G.js`, `/build/chunk-bWyhc9VP.js`, `/build/chunk-B26mR3AF.js`, `/build/chunk-D9X3rG5m.js`, `/build/chunk-tOneJ2Jc.js`, `/build/main-NJFDD5X2.js`, `/build/chunk-BmLaGOhR.js`, `/build/chunk-Cgkukc-o.js`, `/build/chunk-8ttKOKRP.js`, `/manifest.webmanifest`, `/build/chunk-PDGZpzDg.js`, `/build/chunk-CcD-7qJl.js`, `/build/chunk-CPGebrjw.js`, `/build/chunk-C1EtaEcg.js`, `/build/chunk-C_dKek7V.js`, `/build/chunk-B1OeOvke.js`, `/build/chunk-DssHLIT1.js`, `/build/chunk-CG4VFWF4.js`.

### Champion (Annie)

- Piste de performance `cache-insight` (Use efficient cache lifetimes) : Est savings of 3,696 KiB.
- Piste de performance `image-delivery-insight` (Improve image delivery) : Est savings of 286 KiB.
- Piste de performance `network-dependency-tree-insight` (Network dependency tree).
- Piste de performance `interactive` (Time to Interactive) : 7.6 s.
- Piste de performance `unused-javascript` (Reduce unused JavaScript) : Est savings of 120 KiB.
- Piste de performance `total-byte-weight` (Avoid enormous network payloads) : Total size was 4,473 KiB.
- 11 ressource(s) texte servie(s) sans compression, 502 Kio : `/build/chunk-BOwvoBif.js`, `/build/chunk-D1YYfR6G.js`, `/build/chunk-bWyhc9VP.js`, `/build/chunk-B26mR3AF.js`, `/build/chunk-D9X3rG5m.js`, `/build/chunk-tOneJ2Jc.js`, `/build/main-NJFDD5X2.js`, `/build/chunk-BmLaGOhR.js`, `/build/chunk-Cgkukc-o.js`, `/build/chunk-8ttKOKRP.js`, `/manifest.webmanifest`.

### Objet (Infinity Edge)

- Piste de performance `image-delivery-insight` (Improve image delivery) : Est savings of 6 KiB.
- Piste de performance `network-dependency-tree-insight` (Network dependency tree).
- Piste de performance `interactive` (Time to Interactive) : 5.4 s.
- Piste de performance `unused-javascript` (Reduce unused JavaScript) : Est savings of 203 KiB.
- 11 ressource(s) texte servie(s) sans compression, 525 Kio : `/build/chunk-BOwvoBif.js`, `/build/chunk-D1YYfR6G.js`, `/build/chunk-bWyhc9VP.js`, `/build/chunk-B26mR3AF.js`, `/build/chunk-D9X3rG5m.js`, `/build/chunk-tOneJ2Jc.js`, `/build/main-NJFDD5X2.js`, `/build/chunk-Cgkukc-o.js`, `/build/chunk-8ttKOKRP.js`, `/manifest.webmanifest`, `/build/chunk-BEmq3_Y9.js`.

### À propos (prérendue)

- Piste de performance `network-dependency-tree-insight` (Network dependency tree).
- Piste de performance `interactive` (Time to Interactive) : 5.2 s.
- Piste de performance `unused-javascript` (Reduce unused JavaScript) : Est savings of 128 KiB.
- 14 ressource(s) texte servie(s) sans compression, 514 Kio : `/build/chunk-BOwvoBif.js`, `/build/chunk-D1YYfR6G.js`, `/build/chunk-bWyhc9VP.js`, `/build/chunk-B26mR3AF.js`, `/build/chunk-D9X3rG5m.js`, `/build/chunk-tOneJ2Jc.js`, `/build/main-NJFDD5X2.js`, `/build/chunk-B2FftdsQ.js`, `/build/chunk-CcD-7qJl.js`, `/build/chunk-FnYswnQx.js`, `/build/chunk-j8wGpWlr.js`, `/manifest.webmanifest`, `/build/chunk-Cgkukc-o.js`, `/api/catalog/16.19.1/en_US/items?page=1&size=1`.

## Prod, à titre indicatif

Mesurée sur https://league-of-data-base.com, mêmes réglages.

| Page | Perf. | Access. | Bonnes pr. | SEO | FCP | LCP | TBT | CLS | Speed Index |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| [Accueil](https://league-of-data-base.com/) | 100 | 100 | 100 | 100 | 1.0 s | 1.7 s | 0 ms | 0.000 | 1.0 s |
| [Liste des champions](https://league-of-data-base.com/champions) | 60 | 98 | 100 | 100 | 1.5 s | 5.1 s | 80 ms | 0.465 | 1.5 s |
| [Champion (Annie)](https://league-of-data-base.com/champion/Annie) | 98 | 100 | 100 | 100 | 1.1 s | 2.4 s | 0 ms | 0.002 | 1.1 s |
| [Objet (Infinity Edge)](https://league-of-data-base.com/object/3031) | 100 | 100 | 100 | 100 | 1.0 s | 1.7 s | 0 ms | 0.000 | 1.0 s |
| [À propos (prérendue)](https://league-of-data-base.com/about) | 100 | 100 | 100 | 100 | 1.0 s | 1.7 s | 0 ms | 0.000 | 1.0 s |

## Détails de la prod

### Accueil

- Piste de performance `cache-insight` (Use efficient cache lifetimes) : Est savings of 191 KiB.
- Piste de performance `image-delivery-insight` (Improve image delivery) : Est savings of 153 KiB.
- Piste de performance `network-dependency-tree-insight` (Network dependency tree).
- Piste de performance `render-blocking-insight` (Render-blocking requests).
- Piste de performance `bootup-time` (Reduce JavaScript execution time) : 4.0 s.
- Piste de performance `mainthread-work-breakdown` (Minimize main-thread work) : 4.7 s.
- 1 ressource(s) texte servie(s) sans compression, 2 Kio : `/manifest.webmanifest`.

### Liste des champions

- accessibility en échec : `heading-order` (Heading elements are not in a sequentially-descending order).
- Piste de performance `cache-insight` (Use efficient cache lifetimes) : Est savings of 867 KiB.
- Piste de performance `cls-culprits-insight` (Layout shift culprits).
- Piste de performance `forced-reflow-insight` (Forced reflow).
- Piste de performance `image-delivery-insight` (Improve image delivery) : Est savings of 458 KiB.
- Piste de performance `lcp-discovery-insight` (LCP request discovery).
- Piste de performance `modern-http-insight` (Modern HTTP) : Est savings of 130 ms.
- Piste de performance `network-dependency-tree-insight` (Network dependency tree).
- Piste de performance `render-blocking-insight` (Render-blocking requests).
- Piste de performance `interactive` (Time to Interactive) : 5.5 s.
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
