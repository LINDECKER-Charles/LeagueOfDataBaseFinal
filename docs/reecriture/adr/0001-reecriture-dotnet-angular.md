# ADR 0001 — Réécrire LeagueOfDataBase en .NET 10 + Angular

- **Statut** : acceptée — 2026-09-24
- **Révise** : la décision « SPA rejetée » citée dans
  [`packaging-apk.md`](../../guides/packaging-apk.md) (TWA de la PWA plutôt qu'un front
  embarqué)

## Contexte

LeagueOfDataBase tourne sur Symfony 7.4 / PHP 8.5-FPM, deux micro-services Go
(`go-fetcher`, `go-api`) et un front Twig + Turbo Drive + îlots Vue 3. Trois besoins ne
passent plus :

- **Portabilité** : une app desktop (Windows, macOS) et Android qui partagent le front du
  web. Les îlots Vue sont montés dans du HTML rendu par Twig : ils ne forment pas une
  application autonome qu'on peut embarquer.
- **Scaling** : sessions, manifeste d'images, analytics, cache de données et rate
  limiters vivent en fichiers sur un seul hôte ; le pool FPM est dimensionné sans métrique.
- **Contournements** : une partie importante du code existe pour compenser le modèle
  PHP-FPM (voir [`heritage.md`](../heritage.md) § 1).

Le rythme de sortie de features est faible : le coût d'une réécriture est accepté.

## Décision

1. **Backend : .NET 10 LTS** (ASP.NET Core, EF Core + Npgsql), en un seul hôte
   ([ADR 0002](0002-hote-serveur-unique.md)).
2. **Front : Angular 22**, un seul workspace pour le web (SSR), le desktop et Android
   ([ADR 0005](0005-web-ssr-urls-et-seo.md), [ADR 0006](0006-front-angular-multi-cibles.md)).
3. **Même dépôt**, nouveaux dossiers (`src/`, `tests/`) à côté de `app/` et `go/`, qui
   restent en service jusqu'à la bascule puis sont supprimés
   ([plan de migration](../plan-migration.md)).
4. **Réécriture, pas portage** : chaque contournement hérité est traité à la racine, pas
   recopié. Les invariants métier et les particularités Data Dragon, eux, sont portés
   tels quels et testés.

### Arborescence cible

```
src/
  LoDb.Domain/          modèle Data Dragon, éditions, règles de builds — pur, sans I/O
  LoDb.Ingestion/       egress filtré, clients Data Dragon / CommunityDragon, ingestion, WebP
  LoDb.Infrastructure/  EF Core (Postgres), IBlobStore, caches, outbox
  LoDb.Api/             hôte ASP.NET Core : API app, API publique /v1, webhooks, workers
  LoDb.Desktop/         hôte Photino + Velopack (ADR 0007, 0008)
  LoDb.Web/             workspace Angular : app web (SSR) + build « shell » + android/ (Capacitor)
tests/
  LoDb.*.Tests/         xUnit v3, Testcontainers
  LoDb.E2E/             Playwright
```

Même découpage que GitHealth (`src/App.GitHealth.*`), pour réutiliser ses outils de
build, de publication et de release.

## Alternatives écartées

| Alternative | Pourquoi non |
|---|---|
| Garder Symfony, ajouter PWA / Capacitor / Tauri | Ne règle aucun contournement FPM ; il faudrait quand même réécrire le front en SPA pour les apps |
| Symfony + FrankenPHP (workers longue durée) | Règle la mémoire partagée, pas les tâches de fond ni le front ; reste 3 langages |
| Tout Blazor (SSR + MAUI Blazor Hybrid) | Un seul langage, mais perd l'écosystème Angular + Capacitor déjà maîtrisé ; payload WASM ; Blazor Hybrid n'a pas de mise à jour du front sans passer par le store |
| Tout Go (étendre `go-api`) | Pas d'équivalent d'Identity / EF Core ; pas de desktop en Go dans les patterns existants |

## Conséquences

- **+** Un processus longue durée : tâches de fond, streaming async, cache mémoire
  partagé, verrous en processus — la majorité des contournements disparaît par
  construction.
- **+** Deux langages ; un contrat unique (OpenAPI → client TypeScript généré) au lieu
  de constantes dupliquées entre PHP, TS et Go.
- **+** Web, desktop et Android partagent composants, état et design system.
- **−** Un runtime Node supplémentaire en prod pour le SSR Angular (ADR 0005).
- **−** Plusieurs mois de reconstruction sans nouvelle feature côté prod ; la stack
  actuelle reste en maintenance corrective seulement (voir les bugs latents à corriger
  tout de suite dans [`heritage.md`](../heritage.md) § 4).
- **−** Toutes les URLs changent (locale en préfixe) : plan de 301 obligatoire.
- **Suivi** : au lot 0, réécrire `CLAUDE.md` (stack, invariants, garde-fous, carte des
  scopes de commit) pour la nouvelle arborescence.
