# Lot 7 — Admin, analytics, audit, contact

Objectif du lot : analytics et audit en Postgres avec leur rétention enfin automatique, un
module admin Angular multi-admin avec MFA, et le formulaire de contact.

Critère de sortie local : chaque panneau et chaque action admin exercés par des tests E2E ;
totaux des panneaux (trafic, audience) égaux entre les deux stacks sur les mêmes agrégats
repris ; purge de rétention observée avec une horloge simulée.

## L7.1 — Analytics

- **Objectif** : capturer les pages vues sans rien coûter à la requête, agréger, retenir
  ce que la CNIL permet, et reprendre les agrégats existants.
- **À lire** : `app/src/Service/Analytics/**`, `app/src/EventListener/RecordRequestListener.php`,
  `app/src/Command/AnalyticsRollupCommand.php`, [`analytics.md`](../../architecture/analytics.md),
  [`legal-info.md`](../../guides/legal-info.md) (durées de conservation), tests PHPUnit
  `Service/Analytics/**`, `go/api/internal/trends/**` (clés d'entité lues par
  `/v1/trends`), `heritage.md` A2, D2, D3.
- **Périmètre** : `src/LoDb.Api/Modules/Analytics/**`, `src/LoDb.Infrastructure/Analytics/**`
  (dont le fichier d'enregistrement `Analytics`), `src/LoDb.Api/Workers/Analytics/**`,
  `src/LoDb.Api/Cli/Analytics*`, `src/LoDb.Web/src/app/core/analytics/**`,
  `docker/next/nginx/server.d/analytics.conf` (location interne cible du miroir),
  `docker/next/nginx/snippets/analytics-mirror.conf` (directive `mirror`, incluse par la
  location des pages de L3.11), tests associés.
- **Conception** :
  - Les pages sont servies par le SSR et souvent par le cache nginx : l'API ne les voit
    pas. Deux captures complémentaires, sans double compte :
    - **pages servies** (arrivées et rechargements, robots compris) : `mirror` nginx vers
      un endpoint interne de l'API, jamais routé publiquement ; le miroir part pour chaque
      requête, réponses servies depuis le cache comprises ; sans corps
      (`mirror_request_body off`) et avec des délais courts (connexion et lecture d'une
      seconde), car nginx ne libère la connexion qu'une fois le miroir terminé ; l'endpoint
      met l'événement en file et répond aussitôt (202) ;
    - **navigations internes** de l'application (qui ne demandent plus de HTML) : balise
      envoyée par le routeur (`navigator.sendBeacon` vers `/api/analytics/view`) à chaque
      fin de navigation, **sauf la première**, déjà comptée par le miroir. Cet endpoint ne
      touche aucun état utilisateur : il est exempté d'antiforgery (une balise ne peut pas
      porter d'en-tête) et limité en débit par IP.
  - Périmètre de capture identique à aujourd'hui : l'accueil et les listes et détails non
    versionnés des quatre ressources, désormais dans toutes les locales. Le statut est
    déduit par l'API (entité connue ou non).
  - **Clés d'entité inchangées** (`{type}:{clé}` exactement comme aujourd'hui, runes
    comprises) : l'historique repris reste continu et les ids de `/v1/trends` ne changent
    pas. La conversion depuis le `canonicalPath` des nouvelles URLs est testée.
  - Champs de l'événement comme aujourd'hui, dont `visitor` (hash de l'IP réelle et de
    l'UA, clé secrète) et le pays (base GeoLite2, `GEOIP_DB_PATH`), plus l'origine de la
    capture. Écriture par `Channel` puis insertion groupée (copie binaire Npgsql).
  - Tâche de partitions : crée les partitions à l'avance, supprime celles qui dépassent la
    rétention (`DROP PARTITION`), efface IP et UA à l'échéance.
  - Agrégats : jours clos en tâche quotidienne, jour courant en continu (quelques
    minutes), upsert idempotent, jamais destructif.
  - Sous-commande `analytics import` : reprend les fichiers `analytics/daily/{date}.json`
    du volume de l'ancienne stack ; les événements bruts (IP, UA, expirants) ne sont pas
    repris.
  - Endpoints de rapport pour l'admin (plages 7, 30, 90 jours, tout ; politique `Admin`).
- **Tests** : analyse d'UA, classification des référents, détection de bot, hash visiteur ;
  clés d'entité identiques à celles de l'ancienne stack pour chaque type ; une arrivée puis
  deux navigations internes = trois vues ; écriture groupée ; partitions avec horloge
  simulée ; agrégats équivalents à ceux de PHP sur un même échantillon d'événements ;
  import ; purge.
- **Dépend de** : L6.2. **Taille** : L.

## L7.2 — Audit : rétention, requêtes, purge, reprise de l'historique

- **Objectif** : un journal d'audit interrogeable par l'admin, conservé six mois, historique
  compris. L'écriture (`IAuditLog`, L4.1) et les appels de chaque module existent déjà.
- **À lire** : `app/src/Service/Audit/**` (`AuditQueryService`, `AuditRollupService`,
  `AuditArchiveStore`), `app/src/Command/AuditRollupCommand.php`, tests PHPUnit
  `Service/Audit/**`, `heritage.md` § 6 (Audit), D3.
- **Périmètre** : `src/LoDb.Api/Modules/Audit/**`, `src/LoDb.Api/Workers/Audit/**`,
  `src/LoDb.Api/Cli/Audit*`, tests associés.
- **Conception** : rétention six mois en tâche quotidienne ; requêtes admin (filtres par
  action, résultat, acteur, période ; activité d'un utilisateur ; ordre anti-chronologique)
  et purge, elle-même auditée ; sous-commande `audit import` qui reprend les six derniers
  mois du journal actuel (fichiers NDJSON de `var/state/audit` et archives du stockage),
  pour que les enquêtes admin ne perdent rien à la bascule.
- **Tests** : rétention avec horloge simulée ; requêtes et pagination ; import d'un
  échantillon NDJSON ; purge auditée.
- **Dépend de** : lot 4. **Taille** : M.

## L7.3 — API admin

- **Objectif** : plusieurs admins avec MFA, et tous les panneaux et actions actuels côté
  serveur.
- **À lire** : `app/src/Controller/Admin/**`, `app/src/Service/Admin/**`
  (`AdminPanelCatalog`, `MonitoringReportService`, `ServiceHealthProbe`),
  `app/src/Security/{AdminAuthenticator,AdminUserProvider}.php`, `app/templates/admin/**`
  (inventaire des panneaux et des actions), `heritage.md` G3.
- **Périmètre** : `src/LoDb.Api/Modules/Admin/**`, `src/LoDb.Api/Cli/Admin*`, tests
  associés.
- **Conception** :
  - Rôle `Admin` et **MFA TOTP obligatoire** : enrôlement (clé, URI de QR code, codes de
    secours), deuxième facteur exigé à la connexion d'un admin ; sous-commande
    `admin create --email` (crée ou promeut, affiche la marche à suivre).
  - Panneaux : vue d'ensemble (app, trafic, stockage), trafic, audience, stockage (comptes
    et tailles, mis en cache), supervision (Postgres, stockage, file d'ingestion, état des
    versions, métriques clés lues dans le processus).
  - Actions : utilisateurs (recherche, bannir avec motif, débannir, supprimer), builds
    (dépublier, supprimer), messages de contact (traiter, rouvrir, supprimer), clients de
    l'API (révoquer, créditer de 1 à 1 000 000 requêtes, avec invalidation du cache de la
    clé par `IApiKeyCache`), dons, journal d'audit (et purge), activité d'un utilisateur,
    agrégation analytics à la demande. Toute action est auditée.
- **Tests** : non-admin → 403 ; admin sans MFA → enrôlement exigé ; chaque action ;
  révocation d'une clé effective immédiatement sur `/v1` ; sous-commande.
- **Dépend de** : L6.3, L7.1, L7.2. **Taille** : L.

## L7.4 — Front admin

- **Objectif** : l'admin dans le pipeline du front, avec le même design system.
- **À lire** : `app/templates/admin/**`, `app/public/admin/{css,js}/**`,
  `app/tests/js/**` (specs `chart-scale`, `chart`, `panels`),
  `app/src/Service/Analytics/Chart/**` (calculs des graphiques SVG).
- **Périmètre** : `src/LoDb.Web/src/app/features/admin/**`, `src/LoDb.Web/public/i18n/admin/**`,
  `tests/LoDb.E2E/specs/admin/**`.
- **Conception** : module paresseux sous `/admin/**` (hors locale), rendu client,
  `noindex`, budget de bundle séparé ; thème Hextech ; textes en français (scope `admin`) ;
  connexion avec défi MFA et écran d'enrôlement ; panneaux chargés à la demande avec délai et
  nouvelle tentative ; graphiques SVG maison (série temporelle avec zoom, déplacement et
  réticule, anneau, sparkline, couleurs de chaleur) portés depuis le rendu serveur actuel,
  sans bibliothèque.
- **Tests** : cas des specs `chart-scale` (20), `chart` et `panels` repris ; garde d'accès ;
  E2E de chaque panneau et de chaque action (critère de sortie du lot).
- **Dépend de** : L7.3. **Taille** : L.

## L7.5 — Contact

- **Objectif** : le formulaire de contact du pied de page.
- **À lire** : `app/src/Controller/Editorial/ContactController.php`,
  `app/src/Entity/ContactMessage.php`, `app/src/Service/Mail/ContactMailer.php`, dialogue de
  `partials/footer.html.twig`, `app/assets/vue/fx/contactDialog.ts`.
- **Périmètre** : `src/LoDb.Api/Modules/Contact/**`,
  `src/LoDb.Web/src/app/features/contact/**` (remplace le composant provisoire de L3.1),
  `tests/LoDb.E2E/specs/contact/**`.
- **Conception** : `POST /api/contact` (catégories bug, avis, critique, commercial ; nom,
  e-mail, sujet, message, locale ; pot de miel ; politique de rate limiting « contact » de
  L4.2 ; antiforgery) ; message stocké dans `contact_messages` ; notification par l'outbox
  (gabarit `ContactNotification`) vers `CONTACT_RECIPIENT` avec Reply-To du visiteur ;
  destinataire vide = pas d'envoi ; toast de confirmation.
- **Tests** : validation, pot de miel, rate limit, mise en file de la notification ; E2E du
  dialogue.
- **Dépend de** : lot 4. **Taille** : S.
