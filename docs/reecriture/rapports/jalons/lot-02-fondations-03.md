# Jalon du lot 2 et des fondations du lot 3 (L3.1 à L3.4)

- **Date** : 2026-09-26.
- **Branche** : `docs/reecriture-dotnet-angular`. Ce jalon intègre la vague des fondations
  sur `d697ad6` : L3.1 (`wt/l3-1-routage-ssr`, fusion `03dfe33`), L3.4 (`wt/l3-4-seo`,
  `db73899`), L3.12 (`wt/l3-12-redirections-heritees`, `aca012b`), L4.2
  (`wt/l4-2-auth-web-jetons`, `d6402ad`) et L4.3 (`wt/l4-3-outbox-email`, `526fa65`). Puis
  contrat régénéré (`f8f6b69`) et API de la stack locale branchée sur Mailpit (`300e218`).
  L3.2, L3.3 et L4.1 étaient déjà intégrées.
- **Poste** : macOS arm64, .NET SDK 10.0.400, Node 26.5 / npm 12.0.2, Docker 29.8.0.
- **Stack** : `lodb-next` reconstruite depuis la racine sur `300e218` (5 services
  `healthy`). Le service `migrate` a appliqué `20260926091745_Lot4Accounts` (1 migration,
  157 ms). Base déjà chargée (16.19.1 ingérée, versions listées jusqu'à 16.1.1 et avant).

Références : [plan §8](../../plan-implementation.md#8-jalons-et-critères-de-sortie),
[lot 2](../../implementation/lot-02-api-catalogue.md),
[lot 3, L3.1 à L3.4](../../implementation/lot-03-web-public.md).

## 1. Fusion

Les cinq branches partent toutes de `d697ad6` et ne partagent aucun fichier : aucune
fusion n'a eu de conflit. `Program.cs` enregistrait déjà `AddSeo`, `AddLegacy`,
`AddAccounts` et `AddLoDbOutbox` : rien à câbler. Ajouts aux fichiers partagés, repris
tels quels : `PackageReference` Google dans `LoDb.Api.csproj` (L4.2) ; MailKit et
ressources incorporées dans `LoDb.Infrastructure.csproj`, copie des instantanés dans
`LoDb.Infrastructure.Tests.csproj` (L4.3). Aucun nouveau projet .NET, aucune dépendance
npm modifiée : `package-lock.json` inchangés, pas de `npm install` à committer.

Le dépôt n'a aucun hook de commit (ni `core.hooksPath`, ni husky, ni lefthook) : rien à
rejouer sur les fichiers fusionnés.

Ajout d'intégration : L4.3 laisse l'envoi éteint sans `LoDb:Mail:Host`. La surcouche
locale `compose.next.override.yaml` passe donc à l'API `LoDb__Mail__Host: mailpit`, le port
`1025` et `Security: None`. `compose.next.yaml` et le déploiement ne changent pas.

## 2. Commandes et résultats

Toutes les commandes sont lancées depuis la racine du dépôt.

| Étape | Commande | Résultat |
|---|---|---|
| Contrat | `npm --prefix src/LoDb.Web run api:generate` | `LoDb.Api_app.json` et client régénérés (routes `/api/account` de L4.2) ; `LoDb.Api_public-v1.json` inchangé ; commité en `f8f6b69` |
| Dérive | `npm --prefix src/LoDb.Web run api:check` | sortie 0, « the API contract matches the committed documents and client » |
| Outil du contrat | `node --test "tools/next/api/**/*.test.mjs"` | 2 réussis sur 2 |
| Build .NET | `dotnet build LoDb.slnx -c Release` | 0 avertissement, 0 erreur |
| Tests .NET | `dotnet test LoDb.slnx` | 1 746 tests : 1 744 réussis, 2 ignorés (run de parité sans `LODB_PARITY_RUN`) |
| Front | `npm --prefix src/LoDb.Web run lint` | OK (ESLint, Prettier, 9/9 tests des règles d'architecture) |
| Front | `npm --prefix src/LoDb.Web run typecheck` | OK |
| Front | `npm --prefix src/LoDb.Web run test` | **1er passage : 3 échecs** sur 663 (`disclosure.spec.ts`), puis 2 passages à 663/663 : échec intermittent, G4 |
| Front | `npm --prefix src/LoDb.Web run build:web` | OK, 168 pages prérendues ; avertissement de budget (§ 4) |
| Front | `npm --prefix src/LoDb.Web run build:shell` | OK ; même avertissement de budget |
| Stack | `docker compose -p lodb-next -f compose.next.yaml -f compose.next.override.yaml up -d --build` | 5 services `healthy`, migration du lot 4 appliquée |
| E2E | `npm --prefix tests/LoDb.E2E run typecheck` | OK |
| E2E | `npm --prefix tests/LoDb.E2E test` | 26 réussis sur 26 (legacy, public, routing, seo), deux fois |
| E2E (mesure) | `npm --prefix tests/LoDb.E2E test -- --repeat-each=10` | 260 réussis sur 260 (18 s) ; pics mémoire dans [`memoire.md`](../memoire.md) |
| Routage | `bash tools/next/routing/check-urls.sh` | 44 URLs sur 44 conformes (statut, `Location`, `Cache-Control`, `X-Robots-Tag`) |

## 3. Échecs

Quatre groupes. Leurs fichiers sont disjoints : ils peuvent être corrigés en parallèle.

### G1 — e-mails de compte morts et liens hors de la grammaire d'URL (L4.2 × L4.3)

**Reproduire** (stack démarrée) :

```bash
curl -s -X POST http://localhost:18080/api/account/register -H 'Content-Type: application/json' \
  -d '{"email":"g1@example.test","username":"g1user","password":"G1-motdepasse-long!","acceptTerms":true,"locale":"fr"}'
sleep 8
docker logs lodb-next-api-1 2>&1 | grep outbox.message.dead
docker exec lodb-next-postgres-1 psql -U lodb -d lodb -Atc \
  "select template, model::text, last_error_code from email_outbox order by id desc limit 1"
```

**Sortie utile** :

```text
Outbox message 1 (confirm_email) is dead after attempt 1: model.invalid.
confirm_email|{"username": "jalonl2", "actionUrl": "http://localhost/fr/verify-email?user=1&token=…",
 "displayName": "jalonl2", "expiresInMinutes": "60"}|model.invalid
```

Mailpit ne reçoit rien. Deux défauts :

1. **Clés du modèle.** L4.2 écrit `username` (et `displayName`, que les gabarits
   n'utilisent pas). Le contrat de L4.3 (`EmailModelKeys`) exige `userName` pour
   `ConfirmEmail`. Le message part donc en `dead` au premier essai. Chaque branche passait
   ses tests : L4.2 simule l'outbox, et L4.3 ne connaît pas l'appelant.
2. **Liens vers le front.** `FrontPages` construit `/{locale}/verify-email`,
   `/{locale}/reset-password?user=&token=`, `/{locale}/login?error=` et `/{locale}/profile`.
   La grammaire de L3.1 (plan, lot 3, L3.1 ; `features/account/account.routes.ts`) place ces pages
   sous `/{locale}/account/…`, avec `reset-password/:token`. Sur la stack,
   `/fr/verify-email`, `/fr/reset-password`, `/fr/login` et `/fr/profile` répondent 404,
   et leurs équivalents `/fr/account/…` 200. Les redirections Google (`GoogleStartEndpoint`,
   `GoogleCallback`) visent les mêmes chemins.

**Fichiers suspects** : `src/LoDb.Api/Modules/Accounts/Links/AccountMail.cs`,
`src/LoDb.Api/Modules/Accounts/Links/FrontPages.cs`, et leurs tests sous
`tests/LoDb.Api.Tests/Accounts/`.

**Correction proposée** : écrire le modèle avec les constantes `EmailModelKeys` et
retirer `displayName`, ou l'ajouter au contrat de L4.3. Aligner `FrontPages` sur
`/{locale}/account/…`. Pour la réinitialisation, choisir entre deux formes :
`account/reset-password/{token}?user={id}`, ou une route front qui lit `user` et `token`
en query. Les deux se tiennent ; la route de L3.1 prend le jeton dans le chemin.
Ajouter un test qui fait rendre un vrai e-mail de compte par le moteur de gabarits de
L4.3, pas par une doublure.

### G2 — ni canonique ni hreflang sur `/en/` (L3.1 × L3.4)

**Reproduire** :

```bash
curl -s http://localhost:18080/en/ | grep -oE '<link[^>]*(canonical|hreflang)[^>]*>|<meta[^>]*robots[^>]*>'
```

**Sortie utile** : rien. Le `<title>` reste celui de `index.html`
(`League of Data Base`). Aucune page n'appelle `inject(Seo).apply(…)` : le service de
L3.4 existe et ses tests passent, mais L3.1 a livré des pages provisoires et L3.4 ne les
touche pas. La page d'erreur porte bien `X-Robots-Tag: noindex` en en-tête, mais n'écrit
pas de balise `robots` (`kind: 'error'`).

**Fichiers suspects** : `src/LoDb.Web/src/app/features/home/` (page d'accueil
provisoire), `src/LoDb.Web/src/app/features/errors/error-page.ts`,
`src/LoDb.Web/src/app/features/editorial/editorial-page.ts` (pages prérendues).

**Correction proposée** : l'accueil appelle `Seo.apply({ title, path: '',
titleFormat: 'raw' })`, la page d'erreur `Seo.apply({ kind: 'error', title })`, et la
page éditoriale `Seo.apply({ title, path })`. C'est le minimum pour que le critère soit
vérifiable avant L3.9 et L3.10, qui garderont ces appels dans leurs pages définitives.
Ajouter un E2E `tests/LoDb.E2E/specs/seo/head.spec.ts` : une canonique, 21 hreflang et
`x-default` sur `/en/`.

### G3 — clés i18n brutes dans l'en-tête et le pied de page (L3.2 × L3.3)

**Reproduire** :

```bash
curl -s http://localhost:18080/en/ | grep -oE '>\s*(about|api)\.[a-z_.]+\s*<' | sort | uniq -c
```

**Sortie utile** : `about.index.title`, `about.data.title`, `about.faq.title` et deux
fois `api.nav.developers`, dans toutes les locales, pages prérendues comprises (`/en/about`).
Tout le reste du chrome est traduit, en `fr` comme en `en`. Ces clés vivent dans les scopes
Transloco `about` et `api`. Seules les pages `editorial`, `developers` et `api-portal`
fournissent ces scopes, par `provideTranslocoScope`. L'en-tête et le pied de page sont
hors de ces pages et ne les chargent jamais. La galerie (`ui/gallery/gallery-strings.ts`)
fournit ses propres chaînes, ce qui masquait le défaut.

**Fichiers suspects** : `src/LoDb.Web/src/app/core/layout/header/header.ts`,
`src/LoDb.Web/src/app/core/layout/footer/footer.ts` (et leurs specs).

**Correction proposée** : `providers: [provideTranslocoScope('about', 'api')]` sur ces
composants, ou déplacer ces libellés dans le catalogue racine. Le rendu SSR doit attendre
les scopes, comme il attend déjà le catalogue racine. Ajouter au spec SSR un cas qui vérifie
qu'aucune clé brute ne sort du chrome.

### G4 — test unitaire intermittent `disclosure.spec.ts`

**Reproduire** : `npm --prefix src/LoDb.Web run test`, répété. Un passage sur trois
échouait ici, pendant que `dotnet test` tournait en parallèle.

**Sortie utile** :

```text
FAIL src/app/core/layout/disclosure/disclosure.spec.ts > Disclosure > folds on a press outside, not inside
TypeError: Failed to execute 'dispatchEvent' on 'EventTarget': parameter 1 is not of type 'Event'.
 ❯ src/app/core/layout/disclosure/disclosure.spec.ts:53:36
```

Les trois cas qui dispatchent un `new Event(…)` échouent ensemble. Lancé seul, le fichier
passe (13/13). Il n'a pas changé depuis L3.2 (`df2bc5c`). Hypothèse : un autre fichier du
même worker remplace le `Event` global, qui n'est alors plus celui de la fenêtre jsdom.
Seul `core/i18n/loading/ssr-catalogues.spec.ts` touche au rendu serveur
(`platform-server`) ; c'est le premier suspect.

**Fichiers suspects** : `src/LoDb.Web/src/app/core/layout/disclosure/disclosure.spec.ts`,
`src/LoDb.Web/src/app/core/i18n/loading/ssr-catalogues.spec.ts`.

**Correction proposée** : construire les événements avec le constructeur de la fenêtre
de l'élément (`new (el.ownerDocument.defaultView!.Event)(…)`), ou isoler le spec SSR.
Vérifier par une dizaine de passages complets.

## 4. Constats sans échec

- **Budget du bundle initial** : 573,30 ko (139,5 ko transférés) pour 500 ko
  d'avertissement. Il faisait 549,20 ko au jalon 1, puis L3.1 a ajouté 24,1 ko. À traiter
  avec L3.11 ou L3.13 (budgets Lighthouse).
- **Origine absolue sans port en local** : `robots.txt` annonce
  `Sitemap: http://localhost/sitemap.xml`, les `<loc>` des sitemaps et les liens d'e-mail
  commencent par `http://localhost/`. nginx transmet `Host: $host`, sans le port. Rien ne
  change derrière l'edge (443). En local, il faut `LoDb__Seo__CanonicalOrigin` et
  `LoDb__Accounts__SiteOrigin`, que fixe L8.1. Les E2E n'en dépendent pas.
- **Compte de test dans la base `lodb-next`** : `jalonl2` (id 1), avec son message
  d'outbox `dead`, créé pour reproduire G1. Un `down -v` le fait disparaître.
- **`features/hello`**, page provisoire du lot 0, n'est plus référencée (signalé par
  L3.1).
- **Colonne `users.locale`** : L4.2 lit la locale des e-mails dans le corps des requêtes,
  faute de colonne. La migration du lot 4 est déjà écrite (L4.1) : décision à prendre, soit
  une colonne au lot 6 (L6.2), soit un écart à consigner.
- **Rétention de `email_outbox`** : aucune purge des messages envoyés (adresse et lien),
  signalé par L4.3.
- **Mémoire** : `web-ssr` culmine à 338 Mio pendant les 260 E2E (limite provisoire
  512m), `api` à 299 Mio (384m).

## 5. Critères

### Lot 2 — vérifié

| Critère | État | Preuve |
|---|---|---|
| Contrat OpenAPI stable | vérifié | `api:generate` n'ajoute que les routes de L4.2 ; `public-v1` inchangé ; un second `api:check` ne trouve aucune dérive |
| Client généré commité | vérifié | `f8f6b69` (`core/api/generated/`, service `AccountService`) |
| Contrôle de dérive actif | vérifié | `api:check` vert ; job `contract` de `.github/workflows/next-ci.yml` (`npm ci`, puis `api:check`) |

### Fondations du lot 3 — non vérifiées (G2, G3)

| Critère | État | Preuve |
|---|---|---|
| Table d'URLs de L3.1 rejouée (statuts et `Location`) | vérifié | `check-urls.sh` : 44/44 ; E2E `specs/routing` 12/12 |
| Traductions présentes dans le HTML rendu en SSR | **partiel (G3)** | chrome traduit en `en` et `fr`, `<html lang="fr">` ; 5 clés des scopes `about` et `api` restent brutes |
| Canonique et hreflang sur `/en/` | **échec (G2)** | aucune balise `canonical`, `hreflang` ni `robots` dans le HTML |
| Sitemaps et `robots.txt` servis par nginx | vérifié | `/robots.txt`, `/llms.txt`, `/sitemap.xml`, `/sitemaps/en/latest.xml` (1 093 URLs), `/sitemaps/fr/16.18.1.xml` : 200 ; anciennes formes : 301 en un saut ; E2E `specs/seo` 7/7 |
| Redirections héritées en un saut | vérifié | E2E `specs/legacy` 5/5 ; par exemple `/16.18.1/object/1004?lang=de_DE` → 301 `/de/16.18.1/items/1004-faerie-charm` → 200 |
| Coquille `<lodb-shell>` rendue | vérifié | `<lodb-shell class="hx-shell …">` dans le HTML SSR de `/en/` et `/fr/` ; E2E `smoke` sans erreur console |

À revérifier après correction : G1 (un e-mail de confirmation arrive dans Mailpit, et son
lien répond 200), G2 (canonique, 21 hreflang et `x-default` sur `/en/`), G3 (aucune clé
brute dans le HTML de `/en/`, `/fr/` et `/en/about`), G4 (dix passages de `test` verts).
Puis les suites complètes, `check-urls.sh` et tous les E2E.
