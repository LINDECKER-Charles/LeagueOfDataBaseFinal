# Réécriture .NET 10 + Angular — dossier de décision

> **Statut** : décisions actées le 2026-09-24, avant la première ligne de code.
> Ce dossier est la source de vérité de la réécriture. Tant qu'elle n'a pas basculé en
> prod, [`../../CLAUDE.md`](../../CLAUDE.md) continue de décrire la stack **en service**
> (Symfony + Go + Vue).

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
| [0003](adr/0003-ingestion-proactive-et-taches-de-fond.md) | **Ingestion proactive** à la sortie d'un patch + tâches de fond (`BackgroundService`) : fin du loader SSE, des placeholders et du polling |
| [0004](adr/0004-stockage-etat-et-scaling.md) | Blobs derrière `IBlobStore` ; **manifeste, analytics et audit dans Postgres** ; aucun état dans le processus web qui empêche de passer à N instances |
| [0005](adr/0005-web-ssr-urls-et-seo.md) | **Angular SSR** (Node) + cache HTTP partagé ; **locale dans l'URL** (`/fr/…`) : hreflang, cache public, sitemaps par langue |
| [0006](adr/0006-front-angular-multi-cibles.md) | **Un workspace Angular, trois coquilles** (web, desktop, Android) ; i18n au runtime (Transloco) ; client API généré depuis OpenAPI |
| [0007](adr/0007-coquilles-desktop-et-android.md) | Desktop : **Photino.NET** (Tauri écarté) ; Android : **Capacitor 8** (TWA et Ionic UI écartés) |
| [0008](adr/0008-mises-a-jour-integrees.md) | Mises à jour intégrées : **Velopack** (desktop), **live update signé du front** + **Play In-App Updates** (Android) |
| [0009](adr/0009-identite-authentification-sessions.md) | **ASP.NET Core Identity** : cookie (web) + jetons (apps), migration transparente des hash, plus de session serveur |
| [0010](adr/0010-observabilite-tests-ci.md) | **OpenTelemetry** + logs JSON + `/metrics` ; Testcontainers, fixtures Data Dragon, Playwright ; un seul contrat OpenAPI |

Plan d'exécution et bascule : [`plan-migration.md`](plan-migration.md). Découpage en chantiers,
conventions et ordre d'exécution : [`plan-implementation.md`](plan-implementation.md).

## Architecture cible

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
      heritage.md        ← contournements hérités → réponse ; invariants à porter
      plan-migration.md  ← lots, critères de sortie, bascule, retour arrière, risques
      plan-implementation.md ← chantiers, conventions, ordre d'exécution, couverture
      implementation/    ← détail des chantiers, un fichier par lot
      adr/               ← une décision par fichier (contexte, décision, alternatives)
