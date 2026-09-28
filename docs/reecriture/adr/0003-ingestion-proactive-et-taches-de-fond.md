# ADR 0003 — Ingestion proactive et tâches de fond

- **Statut** : acceptée — 2026-09-24
- **Remplace** : ingestion à la demande, `DeferredImageIngestor` (`kernel.terminate`),
  loader SSE, placeholders + polling `/api/images/{type}`, warm-up au déploiement

## Contexte

Aujourd'hui, une image Data Dragon n'est récupérée **que lorsqu'un visiteur la
demande**. Tout un appareillage existe pour masquer ce coût :

- le loader SSE « gate-then-visit », soit environ 1 200 lignes côté front, avec un
  watchdog d'inactivité ;
- l'ingestion différée après la réponse (`kernel.terminate`), qui garde un worker FPM
  occupé plusieurs secondes ;
- un état `pending` à trois valeurs, un endpoint de polling (48 noms par appel, backoff
  de 1,5 s à 24 s), et un `retry=1` ;
- le transcodage WebP dans la requête, image par image.

PHP n'a **ni worker ni planificateur**, d'où plusieurs conséquences :

- les e-mails partent de façon synchrone dans la requête ;
- les rollups analytics et audit ne tournent qu'au déploiement, donc `/v1/trends` n'est
  frais qu'au dernier déploiement ;
- la purge de rétention CNIL n'est **jamais planifiée** ;
- rien ne détecte un nouveau patch : c'est le premier visiteur qui paie l'ingestion.

## Décision

### Tâches de fond

Des `BackgroundService` dans `LoDb.Api`, alimentés par des `Channel<T>` bornés. Chaque
tâche périodique repose sur un `PeriodicTimer` et prend un **verrou consultatif
Postgres** (`pg_try_advisory_lock`) : elle ne s'exécute qu'une fois, même à N instances.
Pas de Quartz.NET tant qu'aucun besoin de planification cron complexe n'apparaît.

| Tâche | Fréquence | Remplace |
|---|---|---|
| Veille de patch (`versions.json`) | 10 min | rien (le premier visiteur payait) |
| Ingestion d'une version | à la détection, file bornée | ingestion à la demande + warm-up au déploiement |
| Transcodage WebP | dans le pipeline d'ingestion, en parallèle | GD image par image, dans la requête |
| Rollups analytics et audit | quotidien, + jour courant en continu | rollup au déploiement seulement |
| Rétention (audit 6 mois, événements bruts à IP/UA) | quotidien | jamais planifiée |
| Expiration des crédits API (12 mois) | quotidien | contractuelle, jamais implémentée |
| Outbox e-mail | continu | envoi synchrone dans la requête |

### Ingestion proactive

1. La veille détecte une nouvelle version dans `versions.json` (en filtrant
   `lolpatch_*`).
2. Elle ingère les **datasets JSON de toutes les langues** puis les **images des quatre
   ressources**, en parallèle borné (`Parallel.ForEachAsync`). Le WebP est produit en
   même temps.
3. **Seulement ensuite**, la version devient « latest » pour le site et les apps. Un
   visiteur ne voit donc jamais une version froide.
4. La **longue traîne des versions anciennes** (397 versions × 28 langues) reste
   ingérée à la demande. Pour ces versions :
   - la page de détail résout ses images de façon synchrone, comme aujourd'hui
     (contrat « de vraies icônes sur une version froide ») ;
   - les listes et aperçus mettent l'ingestion en file et renvoient les placeholders ;
   - l'avancement est poussé au client par SSE, en async, sans thread bloqué, si une
     interface d'attente reste utile.
5. Les crawlers qui parcourent les sitemaps de versions anciennes passent par une
   **file bornée et limitée en débit** : ils ne peuvent plus saturer le serveur.

Traitement d'image : **SkiaSharp** (MIT). ImageSharp est écarté à cause de sa licence
Six Labors Split (commerciale au-delà d'un seuil).

### Règles d'ingestion portées telles quelles

- Absence définitive (403/404) : **persistée**, dans le manifeste
  ([ADR 0004](0004-stockage-etat-et-scaling.md)) ou en dataset vide (`runesReforged`
  avant 7.22.1). Erreur transitoire : **jamais persistée**, ni vide ni absente.
- Dataset manquant dans une langue : on se replie sur `en_US`, seule langue présente
  dans toutes les versions.
- Un seul log de synthèse par lot, jamais une ligne par URL.

## Alternatives écartées

| Alternative | Pourquoi non |
|---|---|
| Garder l'ingestion à la demande, sans le détour FPM | Garde tout l'appareillage de loader, de placeholders et de polling, et les crawlers continuent de déclencher des ingestions à froid |
| Tout pré-ingérer (397 versions × 28 langues) | Coût disque et réseau disproportionné pour des versions que presque personne ne consulte |
| Quartz.NET / Hangfire | Surdimensionné pour 7 tâches à fréquence fixe ; le verrou consultatif Postgres suffit |
| Worker dans un conteneur séparé | Pertinent seulement si l'ingestion concurrence le trafic web ; le code s'y prête (même solution), à décider sur métriques |

## Conséquences

- **+** Suppression du loader SSE, de `DeferredImageIngestor`, du polling, du `retry=1`
  et du warm-up au déploiement : moins de code, et des pages chaudes dès l'exposition
  d'un patch.
- **+** Rétention CNIL, fraîcheur de `/v1/trends` et expiration des crédits deviennent
  enfin automatiques.
- **+** Chaque tâche est observable : durée, erreurs et taille de file exposées en
  métriques ([ADR 0010](0010-observabilite-tests-ci.md)).
- **−** Un patch n'apparaît qu'une fois ingéré, quelques minutes après sa publication
  par Riot. C'est assumé : mieux vaut un patch complet qu'un patch immédiat mais froid.
