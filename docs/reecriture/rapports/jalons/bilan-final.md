# Bilan final de la réécriture (.NET 10 + Angular 22)

- **Date** : 2026-09-27.
- **Branche** : `docs/reecriture-dotnet-angular` (non poussée), contrôlée après la
  [vérification du lot 8](lot-08.md#5-vérification).
- **Référence** : [plan §8 et §9](../../plan-implementation.md#8-jalons-et-critères-de-sortie),
  [§11](../../plan-implementation.md#11-opérations-hôte-et-humaines).
- **Sources** : les sections « Vérification » des rapports de ce dossier ; chaque état
  ci-dessous renvoie à la sienne.

**Verdict** : les 59 chantiers sont livrés et fusionnés. Les suites .NET, front, contrat
et E2E sont vertes. Les critères de sortie des lots 0 à 2 et 4 à 10 sont vérifiés.
Le **critère du lot 3 reste bloqué** par les budgets Lighthouse (performance et LCP). La
définition de terminé est donc remplie, à cette réserve près.

## 1. Définition de terminé (§9), point par point

| Exigence | État | Preuve |
|---|---|---|
| Tous les chantiers livrés et fusionnés | **tenu** | § 3 ; `git branch --no-merged` ne liste que `main`, aucune branche `wt/*` ni branche de vague |
| Arbre de travail propre | **tenu** | `git status --short` vide après le dernier commit ; `.sillage/` jamais indexé |
| Suites .NET vertes | **tenu** | `dotnet build LoDb.slnx -c Release` : 0 avertissement, 0 erreur ; `dotnet test LoDb.slnx` : 2 937 réussis, 2 ignorés (parité sans `LODB_PARITY_RUN`), 0 échec |
| Suites front vertes | **tenu** | `lint`, `typecheck` OK ; `test` 233 fichiers, 2 039 tests ; `build:web` (168 pages prérendues) et `build:shell` OK, avec l'avertissement de budget du bundle initial (654,79 ko pour 500 ko) |
| Contrat | **tenu** | `api:check` sortie 0, aucune dérive ; `tools/next/contract/check.sh` : `CONTRACT OK` |
| E2E verts | **tenu** | `lodb-next` reconstruite sur `8fba30a`, `docker restart lodb-next-api-1`, puis `npm --prefix tests/LoDb.E2E test` : 302 tests, 301 réussis, 1 ignoré, 0 échec ([lot 8](lot-08.md), § 5.1) |
| Critères de sortie des lots 0 à 10 | **tenu sauf lot 3** | § 2 ; lot 3 bloqué, raison consignée |
| Rapports présents | **tenu** | § 4 |
| Runbook et opérations hôte à jour | **tenu** | [`bascule.md`](../../bascule.md) § 1 et § 3.1 corrigés par G5 et suivis tels quels à la répétition rejouée ; § 6 ci-dessous |
| L'ancienne stack démarre et ses tests passent | **tenu** | démarrée depuis la racine ; PHPUnit 695 tests OK, Vitest de `app/` 290 tests, tests Go (`golang:1.26`) tous `ok` ; puis arrêtée ([lot 8](lot-08.md), § 5.5) |

## 2. État de chaque lot

| Lot | Critère de sortie local | État | Preuve |
|---|---|---|---|
| 0 | Suites vertes ; `/en/` en SSR ; `/healthz`, `/readyz`, `lodb_build_info` ; logs JSON d'une ligne | **vérifié** | [lot 0](lot-00.md), § 6.3 |
| 1 | Parité PHP / .NET sur 10 versions × 5 langues + pièges ; patch ingéré sans intervention, WebP | **vérifié** | [lot 1](lot-01.md), § 6 ; [`parite-lot-1.md`](../parite-lot-1.md) : 12 260 écarts, 0 non classé, 0 défaut de la nouvelle stack |
| 2 | Contrat OpenAPI stable, client commité, dérive contrôlée | **vérifié** | [lot 2](lot-02-fondations-03.md), § 6.4 ; `api:check` vert à chaque intégration depuis |
| 3 | Diff SEO ; E2E et accessibilité verts ; budgets Lighthouse | **bloqué** | diff SEO : 42 pages, 0 écart non expliqué ; E2E : suite complète verte ; axe 26/26, accessibilité Lighthouse 98 à 100. **Budgets non tenus** : remesure du 2026-09-27 (`005cb57`), 5 pages sur 5, performance 73 à 87, LCP 3,2 à 5,4 s ([`lighthouse.md`](../lighthouse.md), [lot 3](lot-03.md), Vérification) |
| 4 | Connexion bcrypt et argon2, hash réécrit vérifié par `password_verify` | **vérifié** | [lot 4](lot-04.md), § 7.5 : 48 cas sur 48 ; rejoué par la répétition (8 formats, § 5.3 du lot 8) |
| 5 | Builds de l'ancienne base à l'identique, fantômes compris | **vérifié** | [lots 5 à 7](lots-05-06-07.md), § 4.1 et § 7.3 ; [`parite-builds.md`](../parite-builds.md) |
| 6 | Contrat `/v1` face à go-api ; webhook rejoué sans double crédit | **vérifié** | [lots 5 à 7](lots-05-06-07.md), § 7.3 ; [`contrat-v1.md`](../contrat-v1.md) |
| 7 | Chaque panneau et action admin par E2E ; totaux égaux entre stacks ; purge de rétention | **vérifié** | totaux et purge : [lots 5 à 7](lots-05-06-07.md), § 7.3 ; E2E admin 29/29 après la correction G2 : [lot 8](lot-08.md), § 5.2 |
| 8 | Répétition locale, retour arrière prouvé | **vérifié** | [lot 8](lot-08.md), § 4 et § 5.3-5.4 : 15 étapes `ok`, code 0 |
| 9 | Build macOS arm64, `--smoke`, mise à jour N-1 → N | **vérifié** | [lots 9 et 10](lots-09-10.md), § 6.3 ; [`spike-desktop.md`](../spike-desktop.md) |
| 10 | APK de debug en conteneur `linux/amd64` ; live update, retour arrière, `426` par tests | **vérifié** | [lots 9 et 10](lots-09-10.md), § 6.3 ; reste hors critère : A1 (APK de debug sans clé publique), § 5 |

**Raison du blocage du lot 3.** Le bundle initial pèse 654,79 ko (161,64 ko en gzip) pour
un budget de 500 ko. Le FCP est de 3,0 à 3,3 s sous le profil mobile de Lighthouse, et la
fiche champion pèse 4 173 Kio. Le second temps de G4 du lot 3 (routes paresseuses,
dépendances chargées au démarrage, image de fond de la fiche champion) n'a pas été fait. Les
budgets et le profil de mesure n'ont pas été relâchés. La prod actuelle, mesurée à titre
indicatif dans le même rapport, n'est pas un critère.

## 3. Les 59 chantiers

« Racine » : chantier seul dans sa vague, commité directement sur la branche d'intégration.
Toutes les branches citées sont fusionnées.

| ID | Chantier | Intégration | État |
|---|---|---|---|
| L0.1 | Solution .NET, hôte `LoDb.Api` | `a9fce30` | livré |
| L0.2 | Workspace Angular | `25ef2dd` | livré |
| L0.3 | Conteneurs et Compose `lodb-next` | racine (`ac343cb`) | livré |
| L0.4 | CI, squelette E2E, déploiement `next` | racine (`ff87a2b`) | livré |
| L0.5 | `CLAUDE.md`, guide de dev | racine (`1fa0885`) | livré |
| L1.1 | Domaine Data Dragon | `wt/l1-1-domaine` | livré |
| L1.2 | Egress filtré | `wt/l1-2-egress` | livré |
| L1.3 | Stockage des contenus | `wt/l1-3-stockage` | livré |
| L1.4 | Persistance, `Baseline`, `migrate`, anonymisation | `wt/l1-4-persistance` | livré |
| L1.5 | Clients DDragon / CDragon | racine (`ec800d2`) | livré |
| L1.6 | Pipeline d'ingestion, WebP | racine (`e9cc37c`, `f32714e`) | livré |
| L1.7 | Catalogue en mémoire | racine (`bbdf6ed`) | livré |
| L1.8 | Parité PHP ↔ .NET | racine (`4ff9df8`) | livré |
| L2.1 | Endpoints du catalogue | `wt/l2-1-endpoints-catalogue` | livré |
| L2.2 | Contrat OpenAPI, client, dérive | racine (`9b56ad8`, `9af9315`) | livré |
| L3.1 | Routage localisé, coquille SSR | `wt/l3-1-routage-ssr` | livré |
| L3.2 | Design system Hextech | `wt/l3-2-design-system` | livré |
| L3.3 | i18n | `wt/l3-3-i18n` | livré |
| L3.4 | SEO | `wt/l3-4-seo` | livré |
| L3.5 | Socle du catalogue | `l3-5-socle-catalogue` | livré |
| L3.6 | Pages champions | `l3-6-champions` | livré |
| L3.7 | Pages objets | `l3-7-l3-8-objets-runes-sorts` | livré |
| L3.8 | Pages runes et sorts | `l3-7-l3-8-objets-runes-sorts` | livré |
| L3.9 | Accueil, sélecteur de version et de langue | `l3-9-accueil-selecteur` | livré |
| L3.10 | Pages éditoriales, changelog public | `l3-10-editorial` | livré |
| L3.11 | Cache nginx, PWA, en-têtes | `l3-11-cache-pwa` | livré |
| L3.12 | Redirections héritées | `wt/l3-12-redirections-heritees` | livré |
| L3.13 | E2E, accessibilité, diff SEO | `wt/l3-13-e2e-seo` | livré ; budgets Lighthouse non tenus (lot 3) |
| L4.1 | Schéma Identity, hasher | `wt/l4-1-identity-schema` | livré |
| L4.2 | Authentification web et jetons | `wt/l4-2-auth-web-jetons` | livré |
| L4.3 | Outbox e-mail | `wt/l4-3-outbox-email` | livré |
| L4.4 | Profil, favoris, bannissement (API) | `l4-4-profil-api` | livré |
| L4.5 | Socle d'authentification front | `l4-5-auth-front` | livré |
| L4.6 | Pages compte et profil | `l4-6-pages-compte` | livré |
| L5.1 | Règles et API des builds | `wt/l5-1-builds-api` | livré |
| L5.2 | Éditeur de builds | `wt/l5-2-editeur-builds` | livré ; spec E2E corrigée à la vérification du lot 8 (`810366d`) |
| L5.3 | Partage, votes, tendances | `wt/l5-3-partage-tendances` | livré |
| L6.1 | Références `/v1` sur go-api | `wt/l6-1-references-v1` | livré |
| L6.2 | Schéma des lots 6, 7, 9, 10 | `wt/l6-2-schema` | livré |
| L6.3 | Module `/v1` | `wt/l6-3-module-v1` | livré |
| L6.4 | Portail des clés, `/developers` | `wt/l6-4-portail-cles` | livré |
| L6.5 | Stripe | `wt/l6-5-stripe` | livré |
| L7.1 | Analytics | `wt/l7-1-analytics` | livré |
| L7.2 | Audit | `wt/l7-2-l7-5-audit-contact` | livré |
| L7.3 | API admin | `wt/l7-3-l7-4-admin` | livré |
| L7.4 | Front admin | `wt/l7-3-l7-4-admin` | livré ; français constant après une action (`wt/corr-l8-admin-i18n`) |
| L7.5 | Contact | `wt/l7-2-l7-5-audit-contact` | livré |
| L8.1 | Déploiement, CD, migrations avant bascule | `wt/l8-1-deploiement` | livré ; `.env.next.example` incomplet (§ 5) |
| L8.2 | Outils de bascule | `wt/l8-2-outils-bascule` | livré |
| L8.3 | Runbook, retour arrière, *contract*, changelog joueurs | racine (`fed0c64`) | livré |
| L9.0 | Politique client, `426` | `wt/l9-0-l9-3-politique-desktop` | livré |
| L9.1 | Spike Photino / Avalonia | `l9-1-l9-2-desktop` | livré |
| L9.2 | Hôte desktop | `l9-1-l9-2-desktop` | livré |
| L9.3 | Plateforme desktop côté front | `wt/l9-0-l9-3-politique-desktop` | livré |
| L9.4 | Velopack, release desktop | `wt/l9-4-velopack` | livré ; release signée : opération hôte |
| L10.1 | Projet Capacitor Android | `l10-1-capacitor` | livré |
| L10.2 | Plateforme Android côté front | `wt/l10-2-plateforme-android` | livré |
| L10.3 | Live update signé, Play In-App Updates | `wt/l10-3-l10-4-android` | livré ; branché dans l'app (correction A1) |
| L10.4 | Build et release Android | `wt/l10-3-l10-4-android` | livré ; release Play : opération hôte |

Les corrections des jalons sont fusionnées de la même façon (`wt/corr-*` et fusions
`chore(...)` des jalons des lots 5 à 10) ; aucune n'est en attente.

## 4. Rapports

| Rapport exigé (§9) | Fichier |
|---|---|
| Schéma de base (lot 1) | [`schema-baseline.md`](../schema-baseline.md) |
| Parité (lot 1), et builds (lot 5) | [`parite-lot-1.md`](../parite-lot-1.md), [`parite-builds.md`](../parite-builds.md) |
| Diff SEO (lot 3) | [`diff-seo.md`](../diff-seo.md) |
| Lighthouse (lot 3) | [`lighthouse.md`](../lighthouse.md), remesuré le 2026-09-27 |
| Complétude i18n | [`i18n-completude.md`](../i18n-completude.md), régénéré le 2026-09-27 : 19 portées, 1 415 clés ; `en` et `fr` complètes ; il manque aux 19 autres locales 407 clés de la racine et les 419 clés de l'admin (servi en français seul) ; chaque clé manquante se replie sur `en` |
| Écarts du contrat `/v1` (lot 6) | [`contrat-v1.md`](../contrat-v1.md) |
| Spike desktop (lot 9) | [`spike-desktop.md`](../spike-desktop.md) |
| Mémoire des stacks | [`memoire.md`](../memoire.md) |

## 5. Échecs et restes persistants

Aucun test ne reste en échec dans les suites complètes. Restent :

| Reste | Lot | Raison | Où le traiter |
|---|---|---|---|
| Budgets Lighthouse (performance, LCP) sur 5 pages sur 5 | 3 | bundle initial de 654,79 ko pour 500 ko, fiche champion lourde ; second temps de G4 du lot 3 jamais confié | `src/LoDb.Web` (`app.config.ts`, `core/`, routes paresseuses), image de fond de la fiche champion |
| `.env.next.example` sans `LODB_PUBLIC_API_ORIGIN`, sans ligne `LoDb__*`, sans variante prod | 8 | fichier à la racine, hors du périmètre des corrections et des vérifications | avant J-3 du runbook |
| APK de debug sans `plugins.LiveUpdate.publicKey` | 10 | `tools/next/android/build-debug.sh` ne transmet pas `LODB_LIVE_UPDATE_PUBLIC_KEY` au conteneur (A1 du jalon des lots 9 et 10) ; l'APK de release est correct | `tools/next/android/build-debug.sh`, `tools/next/android-release/lib/release-in-container.sh` |
| JSON mal formé sur `/api/account/register` → 500 au lieu de 400 | 2 | constat du jalon 2, jamais confié ; revérifié le 2026-09-27 (`{"email":` → 500) | `src/LoDb.Api/Hosting` |
| `autoCsp` encore prescrit par le plan (lot 0, G3) | 0 | documents du plan hors de tout périmètre d'écriture de la tâche ; sans effet sur le code (CSP par nginx) | `docs/reecriture/implementation/lot-00-socle.md:96`, `lot-03-web-public.md:340` |
| `src/LoDb.Web/.prettierignore` n'ignore pas `/android` | 4 | propriétaire L0.2, jamais repris ; `lint` passe aujourd'hui | `src/LoDb.Web/.prettierignore` |
| En-tête comprimé entre `md` et environ 1 150 px | 3 | constat de la correction G1 du lot 3, hors échec | `core/layout/` |
| Quotas en mémoire (inscriptions, contact : 5 par heure) | 3 à 8 | pas un défaut : redémarrer l'API entre deux suites complètes ; un 3ᵉ passage enchaîné bute sur le quota de contact | `CLAUDE.md`, pièges du jalon 3 |
| Non couverts en local : dump réel chiffré, connexion à l'ancienne stack d'un compte créé par la nouvelle, requêtes `edge-*` de `queries.logsql` (`infra-vps` illisible pour la tâche) | 8 | exigent l'hôte ou un essai à la main (`--keep`) | runbook § 3.2, opérations hôte |

## 6. Opérations hôte restantes

Aucune n'a été faite ; rien n'est sorti du poste (ni push, ni déploiement, ni secret réel).
La liste détaillée, datée par rapport au jour de bascule, est au § 1 du
[runbook](../../bascule.md) ; le plan les résume au § 11.

- **CI et déploiement** : pousser la branche d'intégration, obtenir `next-ci.yml` vert et
  les images `:<sha>` ; hôte `next` (DNS, secrets `NEXT_*`, `ENV_NEXT`, dump anonymisé) ;
  environnement `production` avec reviewers et secrets `PROD_NEXT_*` ; certificat Data
  Protection propre à la prod.
- **Services tiers** : relais SMTP et SPF/DKIM/DMARC ; Stripe (clé live, `whsec_` de
  l'endpoint existant ; clés de test sur `next`) ; Google OAuth (URI de retour ajoutée sans
  retirer l'ancienne ; clients Android et desktop) ; Search Console.
- **Edge et surveillance** (`infra-vps`) : labels Caddy, reprise des domaines, collecte de
  `api:9464`, requêtes `edge-*` à confirmer, tableaux Grafana ; mémoire et disque de l'hôte
  pour la fenêtre.
- **Bascule** : répétition avec le dump réel chiffré sur `next`, gel, fenêtre, surveillance
  72 h, désactivation du workflow de l'ancienne prod, *contract* à J+30, décommission.
- **Apps** : Apple Developer ID et notarisation, Azure Artifact Signing (environnement
  `desktop-release`) ; compte Play Console, clés d'envoi, de signature et RSA des bundles
  (sauvegarde hors ligne), vérification développeur, empreintes d'`assetlinks.json`,
  environnement `android-release` ; porte sur émulateur de `next-release-android.yml` en
  CI ; fichiers `.deploy/android` de la prod. Les secrets sont listés dans
  [`github-actions-secrets.md`](../../../guides/github-actions-secrets.md) et les guides de
  release.

## 7. État laissé

- `lodb-next` arrêtée (`docker compose -p lodb-next -f compose.next.yaml -f compose.next.override.yaml stop`),
  volumes gardés (base, stockage, cache de pages), images `8fba30a`.
- Aucun emplacement `lodb-next-e1` ni `lodb-next-e2` (ni conteneur ni volume).
- Ancienne stack arrêtée (6 conteneurs `Exited (0)`), sur sa base `lodb` ; la copie
  `lodb_j567` du jalon des lots 5 à 7 reste dans son PostgreSQL.
- Image `golang:1.26` tirée pour les tests Go ; image `lodb-android-build:local` et ses
  volumes de cache gardés.
- Branche `docs/reecriture-dotnet-angular` non poussée ; worktrees de la tâche gardés sous
  `.sillage/worktrees/`.
