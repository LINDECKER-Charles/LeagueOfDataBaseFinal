# ADR 0012 — Audience : Umami à côté de la mesure maison

- **Statut** : acceptée — 2026-10-11, consignée le 2026-10-22 d'après le code et le changelog
  du 2026-10-11 (`2026-10-11-mesure-audience-umami.md`)
- **Révise** : [ADR 0004](0004-stockage-etat-et-scaling.md) (`/v1/trends` ne lit plus seulement
  `analytics_daily` ; table `audience_entity_daily`), [ADR 0003](0003-ingestion-proactive-et-taches-de-fond.md)
  (tâche horaire `audience.trends`)
- **Détail** : [`analytics.md`](../../architecture/analytics.md)

## Contexte

La mesure maison, portée de l'ancienne stack, compte les pages par le miroir de nginx et une
balise du navigateur, jusqu'à `analytics_event` puis aux agrégats `analytics_daily`. Elle
alimente le panneau d'administration et `/v1/trends`. Ses deux points de capture sont les
seuls points d'accès de l'API sans autorisation (`AllowAnonymous`), donc sans le garde XSRF.
L'hôte fait déjà tourner un Umami partagé (`infra-vps`) : une mesure sans cookie, dans le
navigateur, qui respecte l'opposition du visiteur.

## Décision

- **Umami mesure l'audience du site en production.** Le calque `compose.umami.yaml`,
  ajouté par le job de déploiement en prod seulement, déclare le site à l'Umami de l'hôte par
  des labels ; l'edge route `/u/api/send` du site vers Umami. Le front y poste lui-même
  (`core/analytics/`), depuis l'hôte canonique seulement, sans jamais charger `/u/script.js`.
  Le tableau de bord est sur `stats.league-of-data-base.com`. Le staging n'a pas de site
  Umami.
- **Ce qui part du navigateur est réduit** : gabarit de route, query limitée à `lang` et
  `version`, référent réduit à son origine ; les pages du compte et de l'administration ne
  sont jamais comptées. L'opposition (`localStorage`, Global Privacy Control) arrête la mesure.
- **Tirage pour `/v1/trends`** : une tâche horaire (`audience.trends`,
  `Workers/Analytics/AudienceTrendsWorker.cs`) lit les vues par page avec un compte Umami en
  lecture seule (team `lodb`) et les range dans `audience_entity_daily` (`day`, `entity` de
  forme `{type}:{clé}`, `views`, `visitors`), avec les règles de résolution de la mesure
  maison. Elle dort tant que l'hôte, l'id du site, l'utilisateur et le mot de passe ne sont
  pas tous posés.
- **Source de `/v1/trends`** (`Modules/PublicApi/Trends/Reading/TrendsSource.cs`) : sans date
  de bascule, les agrégats de la mesure maison seuls. Avec `LoDb__Audience__TrendsSwitchDate`
  (`aaaa-mm-jj`, `.env` de la prod), `analytics_daily` pour les jours d'avant, Umami à partir
  de cette date. La collecte d'Umami étant publique, le jour d'une entité y compte au plus
  3 fois sa médiane sur 28 jours (`ClipFactor`, `ClipWindowDays`). La date se pose une fois
  que deux semaines de tirage concordent avec la mesure maison ; la retirer revient à la
  mesure maison.
- **La mesure maison reste** pour le panneau d'administration et jusqu'à la bascule des
  tendances. Ses deux captures `AllowAnonymous` sont des exceptions voulues, tenues fermées par
  un test (`tests/LoDb.Api.Tests/Hosting/AnonymousEndpointsTests.cs`), et partent avec elle.

## Alternatives écartées

| Alternative | Pourquoi non |
|---|---|
| Garder la seule mesure maison | Deux points d'accès sans garde XSRF à tenir, une capture côté serveur qui ignore l'opposition du visiteur |
| Charger le script d'Umami (`/u/script.js`) | Du code de l'outil exécuté avec les droits du site |
| Basculer `/v1/trends` sur Umami d'un coup | Une source publique, gonflable, à comparer d'abord à la mesure maison ; d'où la date de bascule et l'écrêtage |

## Conséquences

- **+** Une audience sans cookie ni service tiers, qui respecte l'opposition du visiteur et
  Global Privacy Control.
- **−** Deux mesures coexistent tant que la mesure maison n'est pas retirée. Le miroir de nginx,
  côté serveur, compte la première page même sous opposition ou Global Privacy Control, que la
  balise et Umami respectent.
- **−** `audience_entity_daily` n'a pas de purge.

## Suite (au 2026-10-22)

- Poser `LoDb__Audience__TrendsSwitchDate` en prod après la comparaison (l'exemple de
  `.env.prod.example` donne 2026-10-25). Le changelog du 2026-10-11 annonçait la mesure
  maison « en place quelques jours » ; elle tourne encore.
- Retirer la mesure maison (miroir, balise, `analytics_event`, ses tâches) et ses deux
  exceptions `AllowAnonymous`, une fois la bascule des tendances faite : aucune date n'est
  fixée.
