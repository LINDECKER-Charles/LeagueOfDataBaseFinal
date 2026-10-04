# Parité des pages `/b/{token}` (L5.3)

Compare, pour chaque build de `tokens.json`, ce que la page partagée affiche sur l'ancienne
stack et sur la réécriture : nom, champion, mode, patch (et patch courant), score d'un
build public, arbres de runes avec clé de voûte et runes, objets de chaque étape avec son
coût, coût total, et marqueurs fantômes. Les deux stacks lisent la **même copie** de la
base de dev de l'ancienne stack, où `fixture.sql` a écrit les builds. Lecture seule : des
GET, sans script ni cookie. L'extraction (`lib/extract.mjs`) lit les classes que les deux
gabarits partagent (`.bshare-head`, `.bshare-tree`, `.keystone__icon`, `.bsteps-node`,
`.bshare-item`, `.forge-ghost`, `.vote-box`) ; les libellés de mode redeviennent des codes
par les catalogues `src/LoDb.Web/public/i18n/*.json`.

```bash
# Une fois : Playwright et son Chromium viennent de tests/LoDb.E2E
npm ci --prefix tests/LoDb.E2E && npm --prefix tests/LoDb.E2E run browsers:install
# Comparer (défauts : --legacy http://localhost:8080, --next LODB_E2E_BASE_URL puis :18080)
node tools/builds-parity/compare.mjs --out docs/reecriture/rapports/parite-builds.md
# Tests de l'outil (pages locales, sans stack)
node --test 'tools/builds-parity/test/*.test.mjs'
```

Options : `--legacy`, `--next`, `--tokens` (défaut `tokens.json`), `--out <rapport.md>`,
`--json <résultats.json>` (lectures brutes et écarts), `--pause-ms` (pause entre deux
pages, défaut 0). Sans `--out`, seuls les écarts et le bilan s'affichent.

Code de sortie : 0 si chaque page montre les mêmes faits des deux côtés, 1 s'il reste un
écart (listé champ par champ : `$.steps[0].items[1].ghost`), 2 si une stack ne répond pas.

## Déroulé du jalon (lots 5 à 7)

1. **Copie de la base.** Restaurer une copie de la base de dev de l'ancienne stack (par
   exemple la copie anonymisée de `tools/db/anonymize.sh`) et y brancher les deux
   stacks. Jamais la base d'une stack qui sert : le script supprime puis réécrit ses lignes.
2. **Deux versions communes.** Choisir `latest` et `older`, présentes sur les deux stacks
   dans les langues de `tokens.json` (`fr_FR`, `en_US`, `de_DE`, `es_ES`, `ko_KR`, `zh_TW`,
   `pt_BR`, `en_GB`) : `app:ddragon:warmup --only=<versions> --langs=<langues>` côté PHP,
   `ingest --version <v>` avec les mêmes langues côté `lodb-dev` (voir
   `tools/parity/README.md`).
3. **Fixture.** Avant ou après la migration de la copie par `lodb-dev` (les colonnes du
   lot 4 ont toutes un défaut) :

   ```bash
   docker exec -i <conteneur postgres> psql -U lodb -d <copie> -v ON_ERROR_STOP=1 \
     -v latest=16.19.1 -v older=15.14.1 -f - < tools/builds-parity/fixture.sql
   ```

   Rejouable : il remplace les comptes `parity_*` et les jetons `feedbeef*`. Il écrit
   10 builds (7 publics, 3 privés ; `sr`, `aram`, `arena`, `nexus_blitz` ; 8 langues ; un
   champion, un objet et une clé de voûte fantômes), 4 comptes (dont un soutien à carte
   publique) et des votes (+2, −1, 0, et un vote sur un build privé qu'aucune page ne
   montre). `tokens.json` ajoute un jeton qu'aucun build ne porte : 404 des deux côtés.
4. **Caches.** La réécriture sert `/b/*` avec `s-maxage=60` : un cache mandataire peut
   garder une page une minute. Lancer la comparaison plus d'une minute après la fixture.
5. **Comparer**, puis traiter chaque écart : corriger la page, ou consigner l'écart voulu
   dans le rapport du jalon avant de relancer. Le rapport n'est jamais retouché à la main.

## Ce qui est comparé, et ce qui ne l'est pas

- Chaque page est demandée avec `?lang=<langue du build>` et un navigateur qui la parle :
  les noms Data Dragon sont alors ceux de la même langue des deux côtés. L'ancienne page
  parle la langue du visiteur, la nouvelle celle du build (L5.3) ; ainsi réglées, elles
  coïncident, et le mode est comparé par son code, pas par son libellé.
- Un fantôme est comparé par son marqueur et le repli qu'il affiche (les deux premiers
  caractères de son identifiant) ; un objet connu, par son nom (`title` de la tuile).
- Hors comparaison : l'étiquette de langue (libellés de l'ancien client contre
  `Intl.DisplayNames`), la date, l'auteur, la description, la tête (`robots`, JSON-LD :
  la nouvelle page est toujours `noindex`, sans JSON-LD, écart assumé du plan § 13 et de
  l'ADR 0005) — le diff SEO (`tools/seo-diff`) en rend compte.

## Tests

`test/fixture.test.mjs` garde `tokens.json` et `fixture.sql` d'accord. `test/compare.test.mjs`
couvre modes, écarts et rapport. `test/extract.test.mjs` sert deux pages locales,
`test/pages/legacy.html` (le gabarit Twig rendu pour un build) et `test/pages/next.html`
(le rendu de `SharePage` pour le même build, celui de `share-page.spec.ts`), et vérifie
dans Chromium qu'elles donnent les mêmes faits ; il est ignoré sans `npm ci` dans
`tests/LoDb.E2E`. Quand un gabarit change, régénérer la page concernée avant de relancer.
