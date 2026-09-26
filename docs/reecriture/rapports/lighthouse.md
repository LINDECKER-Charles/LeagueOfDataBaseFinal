# Lighthouse : budgets du lot 3 (L3.13)

- **Date** : 2026-09-26.
- **Réécriture** : http://localhost:18180 (emplacement e1).
- **Outil** : Lighthouse 13.5.0, HeadlessChrome/153.0.0.0.
- **Profil** : mobile par défaut de Lighthouse (écran 412 × 823, CPU ralenti 4 fois, 4G
  lente simulée : 150 ms de RTT, 1,6 Mbit/s), stockage vidé avant chaque passe.
- **Passes** : 3 par page ; chaque chiffre est la médiane des passes, les
  détails viennent de la passe de performance médiane.
- **Commande** : `node tools/next/lighthouse/run.mjs --next http://localhost:18180 --stack "emplacement e1"`.

Rapport généré : ne pas le modifier à la main, relancer `tools/next/lighthouse/run.mjs`.

## Budgets

Performance ≥ 90 ; Accessibilité ≥ 95 ; SEO ≥ 95 ; LCP ≤ 2500 ms ; CLS ≤ 0.1.

## Réécriture

| Page | Perf. | Access. | Bonnes pr. | SEO | FCP | LCP | TBT | CLS | Speed Index | Budgets |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|:---|
| [Accueil](http://localhost:18180/en/) | 68 | 100 | 100 | 100 | 4.9 s | 5.3 s | 45 ms | 0.007 | 4.9 s | **✗** Performance, LCP |
| [Liste des champions](http://localhost:18180/en/champions) | 73 | 98 | 100 | 100 | 4.2 s | 4.7 s | 35 ms | 0.001 | 4.2 s | **✗** Performance, LCP |
| [Champion (Annie)](http://localhost:18180/en/champions/Annie) | 62 | 100 | 100 | 100 | 5.1 s | 8.7 s | 43 ms | 0.009 | 5.1 s | **✗** Performance, LCP |
| [Objet (Infinity Edge)](http://localhost:18180/en/items/3031-infinity-edge) | 70 | 100 | 100 | 100 | 4.8 s | 4.9 s | 27 ms | 0.011 | 4.8 s | **✗** Performance, LCP |
| [À propos (prérendue)](http://localhost:18180/en/about) | 74 | 100 | 100 | 100 | 4.2 s | 4.5 s | 20 ms | 0.001 | 4.2 s | **✗** Performance, LCP |

**Verdict** : budgets manqués sur 5 page(s) sur 5 : Accueil, Liste des champions, Champion (Annie), Objet (Infinity Edge), À propos (prérendue).

## Détails de la réécriture

### Accueil

- Piste de performance `network-dependency-tree-insight` (Network dependency tree).
- Piste de performance `interactive` (Time to Interactive) : 5.5 s.
- Piste de performance `max-potential-fid` (Max Potential First Input Delay) : 140 ms.
- Piste de performance `unused-javascript` (Reduce unused JavaScript) : Est savings of 124 KiB.
- Piste de performance `bootup-time` (Reduce JavaScript execution time) : 4.2 s.
- Piste de performance `mainthread-work-breakdown` (Minimize main-thread work) : 4.9 s.
- 14 ressource(s) texte servie(s) sans compression, 513 Kio : `/build/chunk-3WoW_BHU.js`, `/build/chunk-jb0uOSO9.js`, `/build/chunk-H1vslIjB.js`, `/build/chunk-a3UzC6uW.js`, `/build/chunk-B_EJK0K4.js`, `/build/chunk-BIZFSpnz.js`, `/build/main-N3PDF2KL.js`, `/build/chunk-DWxT4gQT.js`, `/build/chunk-BoWEFcz-.js`, `/manifest.webmanifest`, `/build/chunk-CAIGwOU5.js`, `/build/chunk-DAhiLxEc.js`, `/build/chunk-CJ6ZSYqx.js`, `/build/chunk-YIx1rjad.js`.

### Liste des champions

- accessibility en échec : `heading-order` (Heading elements are not in a sequentially-descending order).
- Piste de performance `network-dependency-tree-insight` (Network dependency tree).
- Piste de performance `interactive` (Time to Interactive) : 6.4 s.
- Piste de performance `unused-javascript` (Reduce unused JavaScript) : Est savings of 154 KiB.
- 19 ressource(s) texte servie(s) sans compression, 627 Kio : `/build/chunk-3WoW_BHU.js`, `/build/chunk-jb0uOSO9.js`, `/build/chunk-H1vslIjB.js`, `/build/chunk-a3UzC6uW.js`, `/build/chunk-B_EJK0K4.js`, `/build/chunk-BIZFSpnz.js`, `/build/main-N3PDF2KL.js`, `/build/chunk-SrpWMrrm.js`, `/build/chunk-BoWEFcz-.js`, `/build/chunk-DPNzRGqL.js`, `/manifest.webmanifest`, `/build/chunk-DpAaLowN.js`, `/build/chunk-CJ6ZSYqx.js`, `/build/chunk-DZDOslHN.js`, `/build/chunk-YIx1rjad.js`, `/build/chunk-Clnysyid.js`, `/build/chunk-PAZW-NX3.js`, `/build/chunk-D_Ygy7Cl.js`, `/build/chunk-xEgdz8xA.js`.

### Champion (Annie)

- Piste de performance `cache-insight` (Use efficient cache lifetimes) : Est savings of 3,696 KiB.
- Piste de performance `image-delivery-insight` (Improve image delivery) : Est savings of 286 KiB.
- Piste de performance `network-dependency-tree-insight` (Network dependency tree).
- Piste de performance `interactive` (Time to Interactive) : 8.7 s.
- Piste de performance `unused-javascript` (Reduce unused JavaScript) : Est savings of 114 KiB.
- Piste de performance `total-byte-weight` (Avoid enormous network payloads) : Total size was 4,525 KiB.
- 17 ressource(s) texte servie(s) sans compression, 548 Kio : `/build/chunk-3WoW_BHU.js`, `/build/chunk-jb0uOSO9.js`, `/build/chunk-H1vslIjB.js`, `/build/chunk-a3UzC6uW.js`, `/build/chunk-B_EJK0K4.js`, `/build/chunk-BIZFSpnz.js`, `/build/main-N3PDF2KL.js`, `/build/chunk-SrpWMrrm.js`, `/build/chunk-BoWEFcz-.js`, `/build/chunk-DPNzRGqL.js`, `/manifest.webmanifest`, `/build/chunk-JeaFiK8r.js`, `/build/chunk-CJ6ZSYqx.js`, `/build/chunk-YIx1rjad.js`, `/build/chunk-PAZW-NX3.js`, `/build/chunk-DezhJOmF.js`, `/build/chunk-DM_lL7T2.js`.

### Objet (Infinity Edge)

- Piste de performance `image-delivery-insight` (Improve image delivery) : Est savings of 6 KiB.
- Piste de performance `network-dependency-tree-insight` (Network dependency tree).
- Piste de performance `interactive` (Time to Interactive) : 5.4 s.
- Piste de performance `unused-javascript` (Reduce unused JavaScript) : Est savings of 193 KiB.
- 22 ressource(s) texte servie(s) sans compression, 656 Kio : `/build/chunk-3WoW_BHU.js`, `/build/chunk-jb0uOSO9.js`, `/build/chunk-H1vslIjB.js`, `/build/chunk-a3UzC6uW.js`, `/build/chunk-B_EJK0K4.js`, `/build/chunk-BIZFSpnz.js`, `/build/main-N3PDF2KL.js`, `/build/chunk-BoWEFcz-.js`, `/build/chunk-DPNzRGqL.js`, `/manifest.webmanifest`, `/build/chunk-DugRrlSm.js`, `/build/chunk-DAhiLxEc.js`, `/build/chunk-CJ6ZSYqx.js`, `/build/chunk-DZDOslHN.js`, `/build/chunk-YIx1rjad.js`, `/build/chunk-Clnysyid.js`, `/build/chunk-PAZW-NX3.js`, `/build/chunk-D_Ygy7Cl.js`, `/build/chunk-BCkjD2W1.js`, `/build/chunk-DezhJOmF.js`, `/build/chunk-Csn4lLYA.js`, `/build/chunk-xEgdz8xA.js`.

### À propos (prérendue)

- Piste de performance `network-dependency-tree-insight` (Network dependency tree).
- Piste de performance `interactive` (Time to Interactive) : 5.3 s.
- Piste de performance `unused-javascript` (Reduce unused JavaScript) : Est savings of 125 KiB.
- 14 ressource(s) texte servie(s) sans compression, 505 Kio : `/build/chunk-3WoW_BHU.js`, `/build/chunk-jb0uOSO9.js`, `/build/chunk-H1vslIjB.js`, `/build/chunk-a3UzC6uW.js`, `/build/chunk-B_EJK0K4.js`, `/build/chunk-BIZFSpnz.js`, `/build/main-N3PDF2KL.js`, `/build/chunk-DyXT5GKm.js`, `/build/chunk-CeoIIcEp.js`, `/build/chunk-YIx1rjad.js`, `/build/chunk-DWxT4gQT.js`, `/manifest.webmanifest`, `/build/chunk-BoWEFcz-.js`, `/api/catalog/16.19.1/en_US/items?page=1&size=1`.

## Prod, à titre indicatif

Mesurée sur https://league-of-data-base.com, mêmes réglages.

| Page | Perf. | Access. | Bonnes pr. | SEO | FCP | LCP | TBT | CLS | Speed Index |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| [Accueil](https://league-of-data-base.com/) | 100 | 100 | 100 | 100 | 1.1 s | 1.1 s | 0 ms | 0.000 | 1.1 s |
| [Liste des champions](https://league-of-data-base.com/champions) | 76 | 98 | 100 | 100 | 1.5 s | 2.5 s | 31 ms | 0.465 | 1.5 s |
| [Champion (Annie)](https://league-of-data-base.com/champion/Annie) | 96 | 100 | 100 | 100 | 1.0 s | 2.7 s | 0 ms | 0.000 | 1.5 s |
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
- Piste de performance `image-delivery-insight` (Improve image delivery) : Est savings of 702 KiB.
- Piste de performance `lcp-discovery-insight` (LCP request discovery).
- Piste de performance `modern-http-insight` (Modern HTTP).
- Piste de performance `network-dependency-tree-insight` (Network dependency tree).
- Piste de performance `render-blocking-insight` (Render-blocking requests).
- Piste de performance `max-potential-fid` (Max Potential First Input Delay) : 200 ms.
- Piste de performance `layout-shifts` (Avoid large layout shifts) : 1 layout shift found.
- 1 ressource(s) texte servie(s) sans compression, 2 Kio : `/manifest.webmanifest`.

### Champion (Annie)

- Piste de performance `cache-insight` (Use efficient cache lifetimes) : Est savings of 3,700 KiB.
- Piste de performance `image-delivery-insight` (Improve image delivery) : Est savings of 311 KiB.
- Piste de performance `network-dependency-tree-insight` (Network dependency tree).
- Piste de performance `render-blocking-insight` (Render-blocking requests).
- Piste de performance `total-byte-weight` (Avoid enormous network payloads) : Total size was 3,894 KiB.
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
