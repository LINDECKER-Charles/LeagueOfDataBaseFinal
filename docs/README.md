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
      audits/            ← index public ; les audits datés restent privés
      produit/           ← SEO technique
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
| [analytics.md](architecture/analytics.md) | Analytics sans base de données (NDJSON local → agrégats journaliers sur le volume de stockage) et panneau `/admin` |
| [api-publique.md](architecture/api-publique.md) | API REST v1 payante servie par le micro-service Go `go-api` |
| [responsive-mobile.md](architecture/responsive-mobile.md) | Stratégie responsive, breakpoints, composants mobile |
| [pieges.md](architecture/pieges.md) | Nouvelle stack : pièges connus, comportements voulus à ne pas « corriger » (build, SSR, E2E, nginx, déploiement) |

## `guides/` — comment l'exploiter

| Doc | Contenu |
|---|---|
| [setup.md](../legacy/docs/guides/setup.md) | *Ancienne stack, archivée* — Prérequis, installation détaillée, dépannage |
| [configuration.md](../legacy/docs/guides/configuration.md) | *Ancienne stack, archivée* — Variables d'environnement et paramètres applicatifs |
| [docker.md](../legacy/docs/guides/docker.md) | *Ancienne stack, archivée* — Référence des commandes de la stack Compose |
| [developpement.md](guides/developpement.md) | Nouvelle stack `lodb-dev` (.NET + Angular) : build et tests, stack d'intégration, ports, emplacements, E2E, dépannage |
| [configuration.md](guides/configuration.md) | Nouvelle stack : inventaire des secrets GitHub, des lignes `.env` de `staging` et de `prod`, des fichiers et volumes de l'hôte et des services externes |
| [github-actions-secrets.md](guides/github-actions-secrets.md) | Nouvelle stack : pipeline CI/CD, `dev` → `test` (déploiement de `staging`) → `main` (promotion en `prod`) |
| [migration-edge-proxy.md](guides/migration-edge-proxy.md) | Edge proxy partagé du VPS : porté par le dépôt d'infrastructure `infra-vps`, ce que ce projet attend de l'hôte et déclare |
| [observabilite.md](guides/observabilite.md) | Chaîne de logs vers Grafana : fonctionnement, ce qu'il ne faut surtout pas déclarer, requêtes LogsQL / PromQL, dépannage |
| [logging.md](guides/logging.md) | Convention de journalisation applicative : clé d'événement, contexte, niveaux, canaux, interdictions |
| [metriques.md](guides/metriques.md) | Contrat des métriques de l'API et du SSR : port 9464, collecte par `observability`, noms, labels, règle de cardinalité |
| [oauth-google-setup.md](guides/oauth-google-setup.md) | Configuration « Sign in with Google » côté Google Cloud Console |
| [packaging-apk.md](../legacy/docs/guides/packaging-apk.md) | *Ancienne stack, archivée* — Distribution Android (TWA / APK) à partir de la PWA |

## `audits/` — constats datés

Les audits datés (constats, plans de correction) ne sont pas publiés : le dépôt est
public. [`audits/README.md`](audits/README.md) dit pourquoi ; une vulnérabilité se signale
en privé ([`../SECURITY.md`](../SECURITY.md)).

> `report/` (métriques de conformité aux règles chiffrées de `AGENTS.md`) est
> **généré** par le skill `archi-report` et git-ignoré : ne pas l'éditer à la main.

## `produit/` — SEO

| Doc | Contenu |
|---|---|
| [seo-indexabilite.md](produit/seo-indexabilite.md) | État des lieux et feuille de route SEO technique |

## `changelog/` — journal technique

Une entrée par feature ou correctif livré, jointe au commit correspondant.
Format et workflow : [`changelog/README.md`](changelog/README.md), gabarit
[`changelog/TEMPLATE.md`](changelog/TEMPLATE.md).
