# ADR 0006 — Front Angular multi-cibles et i18n

- **Statut** : acceptée — 2026-09-24
- **Remplace** : îlots Vue 3 + PrimeVue montés dans Twig, catalogues YAML Symfony

## Contexte

Le même front doit tourner en trois endroits : le web (SSR), le desktop (Photino) et
Android (Capacitor). Seules quelques capacités diffèrent d'une coquille à l'autre :
liens externes, téléchargements, partage, mises à jour.

Le front actuel a accumulé des pièges propres à Turbo et aux îlots (cf.
[`heritage.md`](../heritage.md) H1 à H4) :

- îlots jamais démontés ;
- snapshot Turbo figé ;
- `data-turbo="false"` sur chaque formulaire ;
- bugs du service worker.

Côté traductions, **seules 2 des 21 locales sont complètes** (en, fr). Les 19 autres se
replient sur l'anglais pour toutes les fonctionnalités récentes.

## Décision

### Une application, deux builds

- **Une seule application Angular** dans `src/LoDb.Web/` :
  - composants standalone et signals ;
  - routes paresseuses par fonctionnalité (catalogue, builds, compte, portail API,
    admin).
- **Build `web`** : SSR, sortie serveur (ADR 0005).
- **Build `shell`** : bundle navigateur statique, tout en `RenderMode.Client`, avec
  l'URL absolue de l'API. Il est embarqué par Photino et par Capacitor (ADR 0007).
- **Couche plateforme** : un jeton d'injection `PLATFORM` et une interface
  `PlatformService`, avec trois implémentations :
  - **web** : comportement navigateur ;
  - **desktop** : pont Photino, `window.external.sendMessage`, requêtes corrélées par id,
    entrées considérées comme non fiables ;
  - **android** : plugins Capacitor.

  La détection se fait une fois au démarrage. **Aucun `if (platform)` hors de
  `core/platform/`.**
- **État** : services + signals. Pas de NgRx : l'état est local aux fonctionnalités, un
  store global serait de la complexité gratuite.

```
src/LoDb.Web/src/app/
  core/        api (client généré), auth, i18n, platform, http (intercepteurs)
  ui/          design system Hextech : composants de présentation, sans logique métier
  features/    catalogue/  builds/  account/  api-portal/  admin/  editorial/
```

Les règles de `CLAUDE.md` s'appliquent telles quelles :

- un composant = présentation + câblage mince ;
- l'orchestration vit dans des services et des fonctions pures, testables sans monter le
  composant ;
- 10 fichiers au plus par dossier.

### Design system

- Les tokens Hextech (`--color-*`, `--font-*`, `--ease-hextech`) et les 4 thèmes
  (`data-theme`) sont portés **en propriétés CSS globales**, sans rien redéclarer en
  dur.
- Les couleurs de jeu (arbres de runes, types de dégâts, raretés) ne varient jamais avec
  le thème.
- **Angular CDK** fournit les primitives accessibles : overlays, bottom sheets,
  dialogues, listbox, drag-and-drop de l'éditeur de builds.
- **Aucun kit UI** : ni PrimeNG, ni Ionic UI (cf. ADR 0007). Le design system couvre
  déjà les usages mobiles (barre de navigation basse, bottom sheets, dès 320 px).
- L'admin devient un module Angular chargé à la demande, avec le même design system,
  sous un budget de bundle séparé. Il ne doit plus être « hors pipeline » : la CI
  garantit que le build front ne casse pas.

### i18n au runtime : Transloco

- Catalogues JSON par locale, chargés à la demande.
- En SSR, le catalogue de la locale de la requête est chargé côté serveur puis transmis
  par transfer state (pas de double fetch).
- Pluriels ICU ; repli sur `en` pour chaque clé manquante.
- **Contrôle de complétude en CI** : un rapport par locale, sans bloquer la CI, pour
  rendre visibles les trous actuels (19 locales incomplètes).
- **RTL pour `ar`** (nouveau) : `dir` dynamique, `Directionality` du CDK, propriétés CSS
  logiques (`margin-inline`…).
- Migration : un script unique convertit les catalogues YAML (`messages`, `seo`, `api`,
  `about`) en JSON. Les clés sont conservées pour garder la traçabilité.

### Contrat API généré

- `LoDb.Api` publie son document OpenAPI au build (`Microsoft.AspNetCore.OpenApi`).
- Un générateur produit des **services `HttpClient`**, par exemple `ng-openapi-gen`. On
  garde ainsi les intercepteurs (auth, en-tête de version client) et le cache de
  transfert SSR.
- **La CI régénère et échoue si le client commité diffère** : la dérive entre back et
  front devient impossible, au lieu des constantes dupliquées actuelles
  (`MAX_NAMES_PER_CALL`, regex de version…).

## Alternatives écartées

| Alternative | Pourquoi non |
|---|---|
| `@angular/localize` (utilisé par GitHealth) | Un build par locale : 21 × 2 cibles. Changer de langue recharge la page, et les apps embarqueraient 21 bundles |
| Deux applications (web et apps) | Duplication de routes et de fonctionnalités ; une couche plateforme suffit |
| NgRx / store global | Pas d'état transverse complexe justifiant sa cérémonie |
| Ionic UI / PrimeNG | Langage visuel concurrent du design system Hextech ; re-thématisation totale pour peu de gain |

## Conséquences

- **+** Web, desktop et Android partagent 100 % des fonctionnalités et du design system.
- **+** Toute la classe de bugs Turbo / îlots disparaît.
- **+** La dérive des contrats back ↔ front est vérifiée en CI.
- **−** Le code spécifique à une plateforme doit rester confiné, ce qui demande une
  discipline vérifiée en revue et par une règle de lint : pas d'import `@capacitor/*`
  hors `core/platform/`.
