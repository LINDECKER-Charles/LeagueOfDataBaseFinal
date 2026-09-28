# Plan de migration

Reconstruction **en parallèle** jusqu'à la parité, puis **bascule du domaine**. On ne
mélange pas des pages Twig et Angular sous un même domaine : sessions et
authentification seraient incompatibles.

## Principes

- **Environnement `next`** :
  - projet Compose distinct (`lodb-next`) sur le même hôte ;
  - sous-domaine dédié, en `noindex`, avec son propre volume `storage` ;
  - déployé **dès le lot 0**, puis à chaque lot.
- **Base de `next`** : dump **anonymisé** de la prod. Seule la répétition de bascule
  (lot 8) utilise un dump réel, chiffré, détruit ensuite.
- **L'ancienne stack reste en maintenance corrective**. Les bugs latents de
  [`heritage.md`](heritage.md) § 4 s'y corrigent **sans attendre** la réécriture.
- **Schéma partagé en expand/contract** :
  - jusqu'à la fin de la période de retour arrière, les migrations EF Core sont
    **additives** ;
  - les hash de mots de passe sont en argon2id, lisibles par PHP (ADR 0009) ;
  - les suppressions (contract) n'arrivent qu'après cette période.
- **Critère de sortie par lot** : un lot n'est terminé que si son critère est vérifié
  sur `next`, pas seulement en local.
- **Changelog** : une entrée `docs/changelog/` par changement visible des joueurs, au
  moment où il arrive en prod. La bascule en regroupe l'essentiel.

## Lots

| Lot | Taille | Contenu | Critère de sortie |
|---|---|---|---|
| **0 — Socle** | M | solution `src/`, workspace Angular, CI (build, tests, typecheck, E2E à vide, dérive OpenAPI), images `lodb-api` / `lodb-web-ssr` / `lodb-nginx`, Compose de dev, logs JSON + OpenTelemetry + santé, **réécriture de `CLAUDE.md`** (stack, invariants, garde-fous, scopes de commit) | pipeline vert ; page SSR « hello » servie sur `next` ; `build_info` et métriques visibles dans Grafana |
| **1 — Données Data Dragon** | L | egress filtré, ingestion proactive + veille de patch, `IBlobStore`, datasets, table `ddragon_asset`, catalogue en mémoire, WebP, **tous les cas UP en tests** | **parité** des datasets et manifestes normalisés, PHP vs .NET, sur les 10 dernières versions × 5 langues + les versions pièges (0.151.2, ~3.13.24, 7.21 / 7.22.1, 8.7) ; un patch complet ingéré sans intervention |
| **2 — API catalogue** | M | listes, détails, recherche, pickers, jumeaux Classic, `/api/client-policy` (squelette), OpenAPI + client TS généré | contrat OpenAPI stable ; client généré commité ; contrôle de dérive actif en CI |
| **3 — Web public** | L | SSR des pages encyclopédie, filtres, recherche, thèmes, i18n (YAML → JSON, 21 locales, RTL), SEO (canoniques, hreflang, JSON-LD, sitemaps, robots, llms), cache nginx, PWA, pages éditoriales prérendues, **résolveur des 301** | diff SEO (titres, canoniques, JSON-LD) sur un échantillon face à la prod ; Lighthouse ≥ prod ; accessibilité et E2E verts |
| **4 — Comptes et profil** | M | Identity sur `users`, hasher de migration + **test croisé PHP**, inscription, connexion, « se souvenir de moi », Google (web), vérification et réinitialisation par outbox, profil, favoris, profil public, suppression, bannissement | connexion réussie avec des comptes réels (bcrypt et argon2) du dump de répétition ; hash ré-écrit vérifié par `password_verify` |
| **5 — Builds et tendances** | M | éditeur (CDK drag-and-drop), règles, partage `/b/`, import sur un autre patch, votes, `/trends` | les builds existants s'affichent à l'identique, fantômes compris |
| **6 — API publique et paiements** | M | `/v1` (clés, quotas, crédits, rate limit), portail des clés, `/developers`, Stripe (dons, packs, abonnements), **idempotence par `event.id`**, expiration des crédits | tests de contrat `/v1` verts face aux réponses de référence de `go-api` ; webhook rejoué sans double crédit |
| **7 — Admin, analytics, audit, contact** | M | module admin (rôle + MFA), analytics et audit en Postgres, rollups, **rétention automatique**, modération, monitoring, contact ; reprise des agrégats journaliers | panneaux admin à parité ; purge de rétention observée sur `next` |
| **8 — Bascule web** | M | voir ci-dessous | 72 h sans régression après la bascule |
| **9 — Desktop** | M | spike Photino / Avalonia (2 j), `LoDb.Desktop`, pont, proxy loopback, Velopack, **signature + notarisation**, E2E N-1 → N | release signée Windows + macOS ; mise à jour automatique observée de bout en bout |
| **10 — Android** | M | Capacitor 8, plugins, App Links + OAuth, live update (bundles signés, retour arrière), piste de test interne Play, In-App Updates | app sur la piste interne ; live update, retour arrière et mise à jour *immediate* validés |

Les lots 9 et 10 **peuvent démarrer dès la fin du lot 3**, puisque le build `shell`
existe. Leur publication publique attend la bascule (lot 8), car les apps ont besoin des
comptes et des builds.

## Bascule (lot 8)

1. **Gel** : l'ancienne stack ne reçoit plus que des correctifs.
2. **Répétition complète sur `next`** avec un dump réel chiffré : migrations
   additives, reprise des agrégats, connexions de comptes réels, E2E, puis destruction
   du dump.
3. **Pré-ingestion** : le volume `storage` de la nouvelle stack est rempli à l'avance
   (dernières versions, toutes les langues). La longue traîne reste à la demande.
4. **Fenêtre de bascule** :
   1. sauvegarde Postgres + instantané du volume `storage` actuel ;
   2. application des migrations additives ;
   3. bascule des labels Caddy : le domaine pointe sur la nouvelle stack ;
   4. activation des 301 héritées ;
   5. smoke tests + E2E en lecture seule sur la prod ;
   6. surveillance renforcée pendant 72 h : 5xx, 404 sur des URLs héritées, Search
      Console, métriques d'ingestion.
5. **Retour arrière** : re-pointer le domaine sur l'ancienne stack, restée déployée et
   arrêtée. C'est possible tant que le schéma reste compatible :
   - les comptes et builds créés entre-temps sont dans les tables communes ;
   - les nouvelles tables (analytics, audit, manifeste) sont ignorées par l'ancienne
     stack.
6. **Contract, après 30 jours sans retour arrière** :
   - suppression de `messenger_messages` et `doctrine_migration_versions` ;
   - passage des horodatages restants en `timestamptz` (UTC). Npgsql est strict sur le
     `DateTimeKind` : d'ici là, un convertisseur de valeur traite l'existant comme de
     l'UTC ;
   - suppression des colonnes mortes.
7. **Décommission** :
   - suppression de `app/`, `go/`, des images et des volumes hérités (après export de
     `var/state`) ;
   - mise à jour de `docs/architecture/`, des guides et de `packaging-apk.md` (remplacé
     par l'ADR 0007).

## Table des 301 héritées

Résolue par `LoDb.Api`, qui s'appuie sur le catalogue pour convertir un ancien
paramètre en id canonique. Locale cible : `en` par défaut. Si l'ancienne URL portait
`?lang=`, la locale correspondante est utilisée (`fr_FR` → `fr`).

| Ancienne URL | Nouvelle URL |
|---|---|
| `/home` | `/en/` (et `/` : 302 selon `Accept-Language`) |
| `/champions`, `/champion/{name}` | `/en/champions`, `/en/champions/{id}` |
| `/objects`, `/object/{name}` | `/en/items`, `/en/items/{id}-{slug}` |
| `/runes`, `/rune/{name}` | `/en/runes`, `/en/runes/{id}-{slug}` |
| `/summoners`, `/summoner/{name}` | `/en/summoners`, `/en/summoners/{id}` |
| `/{version}/{ressource}/{name}` | `/en/{version}/{ressources}/{id}` |
| `/u/{username}`, `/trends`, `/about`, `/about/data`, `/faq`, `/changelog`, `/developers`, `/donate`, `/legal/*` | même chemin sous `/en/` |
| `/sitemap.xml`, `/sitemaps/latest.xml` | index des sitemaps par locale |
| `/login`, `/register`, `/profile/*`, `/reset-password/*` | `/{locale}/account/*` (noindex) |
| `/b/{token}`, `/v1/*`, `/webhooks/stripe`, `/cdn/blobs/*` | **inchangées** (contrats) |

## Risques

| Risque | Impact | Parade |
|---|---|---|
| Perte de référencement à la bascule | trafic | 301 exhaustives, hreflang, suivi Search Console et 404 héritées pendant 72 h |
| Effet tunnel (des mois sans livraison) | projet | critères de sortie par lot, `next` déployé dès le lot 0, bugs de l'existant corrigés en parallèle |
| Écart de parité Data Dragon | contenu faux | tests de parité du lot 1 ; cas UP en tests unitaires |
| Photino abandonné | desktop | `IDesktopShell`, plan B Avalonia 12, spike au lot 9 |
| Faille `@angular/ssr` | sécurité | SSR sans cookie ni secret, `allowedHosts`, Dependabot prioritaire |
| Refus Play (fonctionnalité minimale, paiements) | Android | bundle embarqué + fonctions natives ; paiements retirés du build store |
| Perte d'une clé de signature | plus de mise à jour des apps | sauvegarde hors ligne, procédure écrite au lot 9 et au lot 10 |
| Charge Postgres (analytics) | performance | partitions journalières, écritures groupées, métriques de la base |
