# Lot 2 — API du catalogue

Objectif du lot : exposer le catalogue sous `/api` avec un contrat OpenAPI stable, et
fournir au front un client TypeScript généré que la CI empêche de dériver.

Critère de sortie local : contrat OpenAPI stable ; client généré commité ; contrôle de
dérive actif en CI.

## L2.1 — Endpoints du catalogue

- **Objectif** : tout ce que les pages publiques, l'éditeur de builds et les apps lisent
  du catalogue, avec des réponses cachables et des erreurs typées.
- **À lire** : `app/src/Controller/Api/{PickerController,ImageStatusController}.php`,
  `app/src/Controller/Resource/*Controller.php` (listes, détails, routes de recherche),
  `Resource/AbstractResourceController.php` (échecs d'un détail), `app/src/Service/Picker/**`,
  `app/src/Service/Catalog/Facet/**` (facettes des listes), `app/src/Dto/ClientData.php`,
  `app/src/Service/Tools/ResourceUrlGenerator.php`.
- **Périmètre** : `src/LoDb.Api/Modules/Catalog/**`, `src/LoDb.Api/Modules/ClientPolicy/**`
  (squelette), `tests/LoDb.Api.Tests/Catalog/**`.
- **Conception** :
  - `GET /api/meta` : versions (prêtes et longue traîne), dernière version, motif de
    version, langues Data Dragon, locales et leur langue par défaut. Avec les enums du
    document OpenAPI, c'est la seule source de ces listes pour le front.
  - Chaque entrée de liste et chaque détail portent leur `canonicalPath`
    (`LoDb.Domain`, L1.1) : le front n'a aucune grammaire de chemin d'entité à écrire.
  - `GET /api/catalog/{version}/{lang}/{champions|items|runes|summoners}` : liste avec les
    champs des cartes et les valeurs de facettes ; pagination `page`/`size` pour la
    première page rendue en SSR, liste complète pour le filtrage côté client.
  - `GET /api/catalog/{version}/{lang}/{type}/{id}` : détail complet, images résolues de
    façon **synchrone**, édition et jumeau (`counterpart` : id + édition), faits dérivés,
    URLs hotlink. Id inconnu → 404 ProblemDetails ; version inconnue → 404 ; motif de
    version invalide → 400. La décision 302 ou 404 appartient au routage (L3.1).
  - `GET /api/catalog/{version}/{lang}/search?q=&types=` : recherche normalisée sans
    accents, résultats plafonnés ; remplace les trois routes `/api/*/search/{name}`
    actuelles (sans consommateur front aujourd'hui, gardées pour les apps).
  - `GET /api/pickers/{champions|items|runes|summoners|skins}?version&lang[&mode][&champion]` :
    projections de l'éditeur de builds et du profil. Les objets excluent Classic, les
    objets indisponibles sur la carte du mode, non achetables, `hideFromAll` ou liés à un
    champion (règles de `ItemOptionsProjector`).
  - Version explicite ancienne : `Cache-Control` public long ; dernière version : court,
    avec `stale-while-revalidate` ; placeholders en attente : court + `Retry-After`.
    ETag sur les listes.
  - `GET /api/client-policy` : squelette lu depuis la configuration (versions minimale et
    dernière par plateforme), complété par L9.0.
- **Tests** (Testcontainers + fixtures rejouées) : chaque endpoint en nominal ; version
  inconnue, motif invalide, id inconnu ; jumeaux Classic sur les objets et les sorts ;
  runes vides avant 7.22.1 ; liste de version froide en placeholders avec `Retry-After` ;
  images synchrones sur un détail ; filtres de mode des pickers ; en-têtes de cache.
- **Dépend de** : lot 1. **Taille** : L.

## L2.2 — Contrat OpenAPI, client TypeScript, contrôle de dérive

- **Objectif** : le document `app` fait foi ; le front ne consomme que le client généré ;
  la CI échoue dès qu'un des deux dérive.
- **À lire** : ADR [0006](../adr/0006-front-angular-multi-cibles.md) (contrat généré),
  `.github/workflows/next-ci.yml` (job `contract` préparé en L0.4).
- **Périmètre** : `src/LoDb.Api/openapi/**` (généré), `src/LoDb.Web/src/app/core/api/**`
  (client généré + câblage), `src/LoDb.Web/ng-openapi-gen.json`, les fichiers appelés par
  les scripts `api:generate` et `api:check` (déclarés par L0.2), la dépendance
  `ng-openapi-gen`.
- **Conception** :
  - `api:generate` : build de l'API avec la propriété de génération OpenAPI activée, puis
    `ng-openapi-gen`. `api:check` : la même chose, puis `git diff --exit-code` sur
    `openapi/*.json` et le client ; c'est ce qu'exécute le job `contract` de L0.4, sans
    modification du workflow.
  - `ng-openapi-gen` : services `HttpClient` par tag, origine lue dans le jeton
    `API_BASE_URL` (les chemins commencent par `/api`), enums générés **aussi en tableaux**
    (`enumArray`) pour disposer des listes à l'exécution ; code généré exclu du lint et
    jamais édité.
  - La liste provisoire des locales de L0.2 est remplacée par le tableau généré ; la
    correspondance locale → langue et le motif de version viennent de `/api/meta`.
  - Règle des artefacts générés (principe 6 du plan) : ce chantier committe la première
    génération ; ensuite, seuls les agents d'intégration régénèrent et committent.
- **Tests** : la dérive est démontrée une fois en local (endpoint modifié → job en échec,
  puis retour arrière) et consignée dans le compte rendu.
- **Dépend de** : L2.1. **Taille** : M.
