# Documentation — LeagueOfDataBase

Index de la documentation du dépôt. Les conventions de code et les invariants
d'architecture font foi dans [`../AGENTS.md`](../AGENTS.md) ; le point d'entrée
contributeur est [`../CONTRIBUTING.md`](../CONTRIBUTING.md).

## Organisation

    docs/
      README.md          ← ce fichier
      contribution.md    ← guide contributeur détaillé (FR / EN / ES)
      reecriture/        ← décisions de la réécriture .NET + Angular (ADR, plan)
      architecture/      ← comment le système fonctionne
      guides/            ← comment l'installer, le configurer, l'exploiter
      audits/            ← constats datés et plans de correction
      briefs/            ← briefs de design
      produit/           ← veille, SEO, communication
      changelog/         ← journal technique interne (une entrée par livraison)
      assets/            ← images et icônes utilisées par le README

## `reecriture/` — réécriture .NET 10 + Angular

Décisions actées le 2026-09-24 pour remplacer Symfony + Go + Vue par un hôte
ASP.NET Core et un front Angular unique (web SSR, desktop, Android). Tant que la bascule
n'a pas eu lieu, les sections ci-dessous décrivent la stack **en service**.

| Doc | Contenu |
|---|---|
| [README.md](reecriture/README.md) | Pourquoi, tableau des décisions, architecture et versions cibles |
| [heritage.md](reecriture/heritage.md) | Contournements hérités → réponse, bugs latents, particularités Data Dragon, invariants à porter |
| [plan-migration.md](reecriture/plan-migration.md) | Lots et critères de sortie, bascule, retour arrière, table des 301, risques |
| [plan-implementation.md](reecriture/plan-implementation.md) | Chantiers de chaque lot, conventions de la nouvelle stack, ordre d'exécution, couverture ; détail par lot dans [implementation/](reecriture/implementation/) |
| [adr/](reecriture/adr/) | Une décision par fichier : hôte, ingestion, stockage, SSR, front, coquilles, mises à jour, identité, observabilité |
| [rapports/](reecriture/rapports/) | Rapports produits pendant la construction : relevés mémoire de la stack ([memoire.md](reecriture/rapports/memoire.md)), rapports de jalon par lot ([jalons/](reecriture/rapports/jalons/), dont [lot 0](reecriture/rapports/jalons/lot-00.md)) |

Pour développer : guide [developpement.md](guides/developpement.md) et
[`../AGENTS.md`](../AGENTS.md).

## `architecture/` — comment ça marche

| Doc | Contenu |
|---|---|
| [architecture.md](architecture/architecture.md) | Vue d'ensemble : services, flux, arborescence |
| [architecture-report.md](architecture/architecture-report.md) | Rapport d'architecture — état avant/après refacto (DRY / SOLID / KISS) |
| [analytics.md](architecture/analytics.md) | Analytics sans base de données (NDJSON local → agrégats journaliers sur le volume de stockage) et panneau `/admin` |
| [api-publique.md](architecture/api-publique.md) | API REST v1 payante servie par le micro-service Go `go-api` |
| [responsive-mobile.md](architecture/responsive-mobile.md) | Stratégie responsive, breakpoints, composants mobile |

## `guides/` — comment l'exploiter

| Doc | Contenu |
|---|---|
| [setup.md](../legacy/docs/guides/setup.md) | *Ancienne stack, archivée* — Prérequis, installation détaillée, dépannage |
| [configuration.md](../legacy/docs/guides/configuration.md) | *Ancienne stack, archivée* — Variables d'environnement et paramètres applicatifs |
| [docker.md](../legacy/docs/guides/docker.md) | *Ancienne stack, archivée* — Référence des commandes de la stack Compose |
| [developpement.md](guides/developpement.md) | Nouvelle stack `lodb-dev` (.NET + Angular) : build et tests, stack d'intégration, ports, emplacements, E2E, dépannage |
| [configuration.md](guides/configuration.md) | Nouvelle stack : inventaire des secrets GitHub, des lignes `.env` de `preprod` et de la prod, des fichiers de l'hôte et des services externes |
| [github-actions-secrets.md](guides/github-actions-secrets.md) | Nouvelle stack : pipeline CI/CD, déploiement de `preprod` depuis `dev`, promotion en prod |
| [migration-edge-proxy.md](guides/migration-edge-proxy.md) | Edge proxy partagé du VPS : porté par le dépôt d'infrastructure `infra-vps`, ce que ce projet attend de l'hôte et déclare |
| [observabilite.md](guides/observabilite.md) | Chaîne de logs vers Grafana : fonctionnement, ce qu'il ne faut surtout pas déclarer, requêtes LogsQL / PromQL, dépannage |
| [logging.md](guides/logging.md) | Convention de journalisation applicative : clé d'événement, contexte, niveaux, canaux, interdictions |
| [oauth-google-setup.md](guides/oauth-google-setup.md) | Configuration « Sign in with Google » côté Google Cloud Console |
| [legal-info.md](guides/legal-info.md) | Checklist des informations légales à trancher avant la prod |
| [packaging-apk.md](../legacy/docs/guides/packaging-apk.md) | *Ancienne stack, archivée* — Distribution Android (TWA / APK) à partir de la PWA |

## `audits/` — constats datés

| Doc | Contenu |
|---|---|
| [performance-audit.md](audits/performance-audit.md) | Audit de la chaîne de chargement DDragon et du cache |
| [performance-fix-prompt.md](audits/performance-fix-prompt.md) | Plan de correction dérivé de l'audit ci-dessus |
| [version-compatibility-audit.md](audits/version-compatibility-audit.md) | Compatibilité des versions Data Dragon — diagnostic et correctifs |
| [observabilite-2026-08-23.md](audits/observabilite-2026-08-23.md) | Journalisation : les quatre verrous qui rendent l'app muette, et le plan d'implémentation en 5 lots |
| [observabilite-fix-prompt.md](audits/observabilite-fix-prompt.md) | Mission d'implémentation dérivée du plan ci-dessus — un lot par session, garde-fous et pièges |

> `report/` (métriques de conformité aux règles chiffrées de `AGENTS.md`) est
> **généré** par le skill `archi-report` et git-ignoré : ne pas l'éditer à la main.

## `briefs/` — briefs de design

| Doc | Contenu |
|---|---|
| [brief-design-pages.md](briefs/brief-design-pages.md) | Contenu attendu par page + charte « Hextech » |
| [brief-design-prompt.md](briefs/brief-design-prompt.md) | Prompt de mission accompagnant le brief |

## `produit/` — veille et diffusion

| Doc | Contenu |
|---|---|
| [analyse-concurrentielle.md](produit/analyse-concurrentielle.md) | Cartographie de l'écosystème data League of Legends |
| [seo-indexabilite.md](produit/seo-indexabilite.md) | État des lieux et feuille de route SEO technique |
| [seo-backlinking.md](produit/seo-backlinking.md) | Stratégie d'acquisition de liens |

## `changelog/` — journal technique

Une entrée par feature ou correctif livré, jointe au commit correspondant.
Format et workflow : [`changelog/README.md`](changelog/README.md), gabarit
[`changelog/TEMPLATE.md`](changelog/TEMPLATE.md).
