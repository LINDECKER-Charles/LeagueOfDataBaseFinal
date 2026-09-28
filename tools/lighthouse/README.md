# Lighthouse : budgets du lot 3 (L3.13)

Mesure cinq pages de la stack avec Lighthouse, profil mobile, dans le Chrome de
Playwright, et les confronte aux budgets du jalon : performance ≥ 90, accessibilité ≥ 95,
SEO ≥ 95, LCP ≤ 2,5 s, CLS ≤ 0,1. La prod est mesurée avec les mêmes réglages, à titre
indicatif seulement : réseau, TLS et CDN diffèrent.
Rapport : [`docs/reecriture/rapports/lighthouse.md`](../../docs/reecriture/rapports/lighthouse.md).

```bash
# Une fois : Lighthouse, chrome-launcher et Playwright viennent de tests/LoDb.E2E
npm ci --prefix tests/LoDb.E2E && npm --prefix tests/LoDb.E2E run browsers:install
# Contre la stack lodb-dev de la racine (ou un emplacement : --next http://localhost:18180)
node tools/lighthouse/run.mjs --stack lodb-dev
# Tests de l'outil
node --test 'tools/lighthouse/test/*.test.mjs'
```

Options : `--next` (défaut `LODB_E2E_BASE_URL`, puis `http://localhost:18080`), `--stack`
(nom affiché dans le rapport), `--prod` (défaut `https://league-of-data-base.com`) ou
`--skip-prod`, `--runs` (passes par page, défaut 3), `--out` (défaut le rapport ci-dessus),
`--json <fichier>` (résultats bruts).

Code de sortie : 0 si les cinq pages tiennent leurs budgets, 1 si l'une en manque un, 2 si
une page ne se mesure pas (Lighthouse n'a pas pu la charger).

## Mesure

- Pages (`lib/pages.mjs`) : l'accueil, la liste des champions, un champion, un objet et une
  page prérendue, chacune avec son équivalent en prod.
- Une requête de chauffe par page, hors mesure : elle rend la page et remplit le cache de
  nginx, comme le ferait un premier visiteur.
- Réglages par défaut de Lighthouse : écran mobile, CPU ralenti 4 fois, 4G lente simulée,
  stockage vidé avant chaque passe. Les domaines tiers connus (Google, Stripe) sont bloqués :
  aucune page n'en charge aujourd'hui, et l'outil ne doit jamais les appeler.
- Chaque chiffre du rapport est la médiane des passes ; les audits en échec, les pistes de
  performance et les ressources texte servies sans compression viennent de la passe de
  performance médiane (`lib/summary.mjs`).
