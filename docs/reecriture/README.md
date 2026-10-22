# Réécriture .NET 10 + Angular — dossier de décision

> **Statut** : décisions actées le 2026-09-24 ; **basculé en prod le 2026-10-04**
> ([`bascule.md`](bascule.md), état en tête). La réécriture est la stack du dépôt, décrite par
> [`../../AGENTS.md`](../../AGENTS.md) et, en vue d'ensemble, par
> [`architecture.md`](../architecture/architecture.md). L'ancienne (Symfony + Go + Vue) est
> archivée sous `legacy/` jusqu'à la décommission, après le *contract* (au plus tôt le
> 2026-11-03).
>
> **Ce qui fait foi ici** :
>
> - les [ADR](adr/) et leurs amendements datés (le plus récent l'emporte) ;
> - `bascule.md`, jusqu'à la décommission ;
> - les invariants des §§ 5 et 6 de `heritage.md`, et la table des 301 de `plan-migration.md` ;
> - deux rapports : `rapports/contrat-v1.md` (écarts du contrat `/v1`, que les tests citent) et
>   `rapports/schema-baseline.md` (jusqu'au *contract*).
>
> `rapports/i18n-completude.md`, `diff-seo.md` et `lighthouse.md` sont réécrits par leurs
> outils. Tout le reste raconte la construction de septembre 2026 et est **historique** : le
> reste de `heritage.md` et de `plan-migration.md`, `plan-implementation.md`, `implementation/`
> et les autres rapports, jalons compris. Ces documents ne s'appliquent plus tels quels.

## Pourquoi réécrire

La stack actuelle a livré le produit, mais trois limites freinent désormais son évolution :

1. **Portabilité.** Un front Twig + îlots Vue ne peut pas devenir une app desktop ou
   Android : l'APK prévu n'est qu'une coquille TWA qui affiche le site en ligne
   ([`packaging-apk.md`](../../legacy/docs/guides/packaging-apk.md)). Web, desktop (Windows, macOS) et
   Android doivent partager **un seul front**.
2. **Scaling.** Tout l'état vit sur le disque d'un seul hôte (sessions, manifeste
   d'images, analytics, cache), et le pool PHP-FPM est dimensionné à l'aveugle
   (`pm.max_children = 20`, sans métrique).
3. **Contournements.** Le modèle PHP-FPM (un processus par requête, rien de partagé)
   a imposé une série de détours : ingestion après la réponse (`kernel.terminate`), flux
   SSE qui immobilisent un worker, service Go à part pour paralléliser les fetchs, verrou
   de session qui sérialise les requêtes… L'inventaire complet est dans
   [`heritage.md`](heritage.md).

La cible réduit aussi la stack de **trois langages (PHP, Go, TS) à deux (C#, TS)** et
l'aligne sur des patterns déjà éprouvés dans GitHealth (desktop Photino + Velopack) et
Bloodborne Legendary Run (Angular + Capacitor + mises à jour Android).

## Décisions

| ADR | Décision |
|---|---|
| [0001](adr/0001-reecriture-dotnet-angular.md) | Réécrire en **.NET 10 LTS + Angular 22** ; PHP, Go, Twig et Vue disparaissent à la bascule |
| [0002](adr/0002-hote-serveur-unique.md) | **Un seul hôte ASP.NET Core** (monolithe modulaire) : absorbe `go-fetcher` et `go-api` ; egress filtré dans le processus |
| [0003](adr/0003-ingestion-proactive-et-taches-de-fond.md) | **Ingestion proactive** à la sortie d'un patch + tâches de fond (`BackgroundService`) : fin des placeholders et du polling ; un flux SSE d'attente revient le 2026-09-28 pour un changement de patch ou de langue (`/api/catalog/{version}/{lang}/warm-up`) |
| [0004](adr/0004-stockage-etat-et-scaling.md) | Blobs derrière `IBlobStore` ; **manifeste, analytics et audit dans Postgres** ; aucun état dans le processus web qui empêche de passer à N instances |
| [0005](adr/0005-web-ssr-urls-et-seo.md) | **Angular SSR** (Node) + cache HTTP partagé ; **locale dans l'URL** (`/fr/…`) : hreflang, cache public, sitemaps par langue |
| [0006](adr/0006-front-angular-multi-cibles.md) | **Un workspace Angular, trois coquilles** (web, desktop, Android) ; i18n au runtime (Transloco) ; client API généré depuis OpenAPI |
| [0007](adr/0007-coquilles-desktop-et-android.md) | Desktop : **Photino.NET** (Tauri écarté) ; Android : **Capacitor 8** (TWA et Ionic UI écartés) |
| [0008](adr/0008-mises-a-jour-integrees.md) | Mises à jour intégrées : **Velopack** (desktop), **live update signé du front** + **Play In-App Updates** (Android) |
| [0009](adr/0009-identite-authentification-sessions.md) | **ASP.NET Core Identity** : cookie (web) + jetons (apps), migration transparente des hash, plus de session serveur |
| [0010](adr/0010-observabilite-tests-ci.md) | **OpenTelemetry** + logs JSON + `/metrics` ; Testcontainers, fixtures Data Dragon, Playwright ; un seul contrat OpenAPI |
| [0011](adr/0011-catalogue-pre-rendu-et-cdn.md) | **Catalogue pré-rendu** : pages des anciens patchs calculées une fois dans un stockage objet Scaleway, servies par nginx avec le SSR en repli (2026-10-20) ; **Cloudflare** devant le site prévu, pas encore construit |
| [0012](adr/0012-audience-umami.md) | **Audience Umami** à côté de la mesure maison ; `/v1/trends` passe sur Umami à une date de bascule (2026-10-11) |

Bascule, retour arrière et *contract* : [`bascule.md`](bascule.md). Plan et découpage de la
construction (historiques) : [`plan-migration.md`](plan-migration.md),
[`plan-implementation.md`](plan-implementation.md).

## Architecture cible

*Schéma du 2026-09-24. Il ne montre ni le stockage des pages pré-rendues, ni le SSR de la
passe, ni Umami, ni les sauvegardes hors hôte ; le blob store est le volume `ddragon` (monté
sur `/srv/storage`), pas l'ancien `storage`. Vue d'ensemble à jour :
[`architecture.md`](../architecture/architecture.md).*

```
                      Caddy — edge partagé du VPS (TLS, infra-vps)
                                        │
                                      nginx ──── /cdn/blobs/  (volume storage, lecture seule)
                                        │        cache HTTP des pages SSR publiques
                  ┌─────────────────────┴──────────────────────┐
           /api  /v1  /webhooks                          toutes les pages
                  │                                            │
      LoDb.Api — ASP.NET Core 10                    web-ssr — Node 24, Angular 22 SSR
      ├─ API app (catalogue, comptes, builds)       ├─ sans état, sans secret, rendu anonyme
      ├─ API publique /v1 (clés, quotas, crédits)   └─ ne parle qu'à LoDb.Api (réseau interne)
      ├─ webhooks Stripe
      └─ workers : veille de patch, ingestion, WebP,
         rollups, rétention, outbox e-mail
                  │                     │
            PostgreSQL 17        volume storage (blobs, datasets)

   Desktop  — Photino + Kestrel loopback + Velopack ──┐
   Android  — Capacitor 8 + live update + Play      ──┴──►  https://league-of-data-base.com/api
```

## Versions cibles

| Composant | Version | Fin de support | Note |
|---|---|---|---|
| .NET / ASP.NET Core / EF Core | 10 LTS | 2028-11-14 | .NET 8 et 9 s'arrêtent le 2026-11-10 |
| Angular (+ `@angular/ssr`) | 22 | LTS jusqu'à juin 2028 | cadence d'une majeure par an depuis v22 |
| Node (SSR) | 24 LTS | avril 2028 | Capacitor 8 exige Node ≥ 22 |
| PostgreSQL | 17 | — | base existante conservée |
| Capacitor | 8 | — | `targetSdk 36` exigé par Play depuis le 2026-08-31 |
| Photino.NET / Photino.Native | 4.0.16 / 4.0.22 (épinglés) | — | risque de maintenance, cf. ADR 0007 |
| Velopack | 1.2.x | — | stable depuis la 1.0 (2026-05-26) |

Sources : [releases .NET](https://dotnetcli.blob.core.windows.net/dotnet/release-metadata/releases-index.json),
[Angular releases](https://angular.dev/reference/releases),
[Capacitor 8](https://capacitorjs.com/docs/updating/8-0),
[Velopack](https://github.com/velopack/velopack/releases),
[Photino.NET](https://github.com/tryphotino/photino.NET/releases) — relevés le 2026-09-24.

## Ce que la réécriture garde

Les particularités Data Dragon, les invariants métier (jumeaux Classic indexés par id,
absence définitive persistée, splash en hotlink, règles de builds…) et les garanties de
confidentialité ne se renégocient pas : ils sont listés dans [`heritage.md`](heritage.md)
et deviennent des tests dès le lot 1 du plan.

## Organisation du dossier

    reecriture/
      README.md          ← ce fichier : pourquoi, décisions, architecture, versions
      adr/               ← une décision par fichier, amendements datés : fait foi
      bascule.md         ← runbook de la bascule (faite), retour arrière, contract : fait foi
                           jusqu'à la décommission
      heritage.md        ← contournements hérités → réponse (historique) ; §§ 5-6 : invariants
      plan-migration.md  ← lots, critères de sortie, risques (historique) ; table des 301
      plan-implementation.md ← chantiers, conventions, ordre d'exécution (historique)
      implementation/    ← détail des chantiers, un fichier par lot (historique)
      rapports/          ← rapports de construction et jalons (historique), sauf contrat-v1,
                           schema-baseline et les rapports générés par les outils
      changelog-bascule/ ← brouillons de changelog de la bascule
