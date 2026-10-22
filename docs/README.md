# Documentation — LeagueOfDataBase

Index de la documentation du dépôt. Les règles de code et les invariants d'architecture font
foi dans [`../AGENTS.md`](../AGENTS.md), chaque décision dans son
[ADR](reecriture/adr/) (amendements datés : le plus récent l'emporte). Le point d'entrée
contributeur est [`../CONTRIBUTING.md`](../CONTRIBUTING.md).

La prod tourne sur la stack .NET + Angular depuis le 2026-10-04. Les docs de l'ancienne stack
(Symfony + Go + Vue) sont archivées sous [`../legacy/docs/`](../legacy/docs/) et ne
s'appliquent pas à la stack en service.

## Organisation

    docs/
      README.md        ← ce fichier
      architecture/    ← comment le système fonctionne
      guides/          ← l'installer, le configurer, l'exploiter
      produit/         ← SEO et catalogue pré-rendu : règle en vigueur, spécifications
      reecriture/      ← ADR et runbook de la bascule (font foi) ; plans et rapports de la
                         construction (historiques)
      changelog/       ← journal technique interne (une entrée par livraison)
      audits/          ← index public ; les audits datés restent privés
      assets/          ← images et icônes utilisées par le README

## `architecture/` — comment ça marche

| Doc | Contenu |
|---|---|
| [architecture.md](architecture/architecture.md) | Vue d'ensemble : services, chemin d'une requête, flux, environnements, dépôt |
| [analytics.md](architecture/analytics.md) | Mesure d'audience : capture maison (miroir nginx, balise) en Postgres, Umami, `/v1/trends`, panneau `/admin` |
| [api-publique.md](architecture/api-publique.md) | API REST `/v1` payante, module `PublicApi` de `LoDb.Api` : contrat, clés, quotas, tarifs, Stripe |
| [responsive-mobile.md](architecture/responsive-mobile.md) | Stratégie responsive du front Angular, RTL, méthode d'audit |
| [pieges.md](architecture/pieges.md) | Pièges connus, comportements voulus à ne pas « corriger » (build, SSR, E2E, nginx, déploiement, précalcul) |

## `guides/` — comment l'exploiter

| Doc | Contenu |
|---|---|
| [developpement.md](guides/developpement.md) | Stack locale `lodb-dev` : build et tests, ports, emplacements, E2E, dépannage |
| [tests.md](guides/tests.md) | Stratégie de test : quelle couche teste quoi, budgets, infrastructure des tests .NET |
| [configuration.md](guides/configuration.md) | Secrets GitHub, lignes `.env` de `staging` et de `prod`, fichiers et volumes de l'hôte, services externes |
| [github-actions-secrets.md](guides/github-actions-secrets.md) | Pipeline CI/CD : `dev` → `test` (déploiement de `staging`) → `main` (promotion en `prod`) |
| [migration-edge-proxy.md](guides/migration-edge-proxy.md) | Edge proxy partagé du VPS (dépôt `infra-vps`) : ce que ce projet attend de l'hôte et déclare |
| [sauvegardes.md](guides/sauvegardes.md) | Sauvegardes hors hôte de la base de prod : réseau `sauvegardes`, rôle, journal, page `/admin/backups` |
| [observabilite.md](guides/observabilite.md) | Chaîne de logs vers Grafana, requêtes LogsQL / PromQL, dépannage |
| [logging.md](guides/logging.md) | Convention de journalisation de l'API et du SSR : clé d'événement, champs, niveaux, interdictions |
| [metriques.md](guides/metriques.md) | Contrat des métriques de l'API et du SSR : port 9464, noms, labels, cardinalité |
| [oauth-google-setup.md](guides/oauth-google-setup.md) | « Sign in with Google » côté Google Cloud Console |
| [release-desktop.md](guides/release-desktop.md) | Release des apps desktop (Velopack, flux signés), lancée à la main |
| [release-android.md](guides/release-android.md) | Release Android (AAB, live update signé), lancée à la main |

## `produit/` — SEO et catalogue pré-rendu

| Doc | Contenu |
|---|---|
| [seo-indexabilite.md](produit/seo-indexabilite.md) | **Fait foi pour le SEO** : cap, règle en vigueur dans le code, règle servie en prod jusqu'à la bascule, mesure, chronologie |
| [seo-une-page-par-etat.md](produit/seo-une-page-par-etat.md) | Spécification de la règle « une page indexée par état » (décidée le 2026-10-22, en vigueur dans le code, en prod à sa bascule) et ce que l'implémentation a tranché |
| [seo-anciens-patchs.md](produit/seo-anciens-patchs.md) | Diagnostic de la désindexation d'août et demande du 2026-10-14 (réalisée, remplacée le 2026-10-22) |
| [pre-rendu-catalogue-cdn.md](produit/pre-rendu-catalogue-cdn.md) | Spécification du catalogue pré-rendu et du CDN : lots, réglages, tests ; état des lots en tête |
| [pre-rendu-admin-ux.md](produit/pre-rendu-admin-ux.md) | Spécification de l'écran `/admin/prerender` (livrée le 2026-10-21) |

## `reecriture/` — décisions et bascule

| Doc | Contenu |
|---|---|
| [README.md](reecriture/README.md) | Pourquoi la réécriture, table des ADR, ce qui fait foi dans ce dossier |
| [adr/](reecriture/adr/) | **Font foi** : une décision par fichier (0001 à 0012), amendements datés, état en tête |
| [bascule.md](reecriture/bascule.md) | **Fait foi jusqu'à la décommission** : runbook de la bascule (faite le 2026-10-04), retour arrière, *contract* ; état daté en tête |
| [heritage.md](reecriture/heritage.md) | Historique, sauf les §§ 5 et 6 : particularités Data Dragon et invariants métier |
| [plan-migration.md](reecriture/plan-migration.md) | Historique, sauf la table des 301 |
| [plan-implementation.md](reecriture/plan-implementation.md), [implementation/](reecriture/implementation/) | Historiques : chantiers de la construction (septembre 2026) |
| [rapports/](reecriture/rapports/) | Rapports de construction et jalons (historiques) ; `contrat-v1.md` (écarts du contrat `/v1`) et `schema-baseline.md` font foi ; `i18n-completude.md`, `diff-seo.md`, `lighthouse.md` sont réécrits par leurs outils |
| [changelog-bascule/](reecriture/changelog-bascule/README.md) | Dernier brouillon de changelog de la bascule (`applications`) |

## `audits/` — constats datés

Les audits datés (constats, plans de correction) ne sont pas publiés : le dépôt est
public. [`audits/README.md`](audits/README.md) dit pourquoi ; une vulnérabilité se signale
en privé ([`../SECURITY.md`](../SECURITY.md)).

> `report/` (métriques de conformité aux règles chiffrées de `AGENTS.md`) est
> **généré** par le skill `archi-report` et git-ignoré : ne pas l'éditer à la main.

## `changelog/` — journal technique

Une entrée par feature ou correctif livré, jointe au commit correspondant.
Format et workflow : [`changelog/README.md`](changelog/README.md), gabarit
[`changelog/TEMPLATE.md`](changelog/TEMPLATE.md).

## Archives de l'ancienne stack

Sous [`../legacy/docs/`](../legacy/docs/), supprimées avec `legacy/` à la décommission :
`guides/` (`setup.md`, `configuration.md`, `docker.md`, `github-actions-secrets.md`,
`packaging-apk.md`, `logging.md`), `architecture/` (`architecture.md`, `analytics.md`,
`responsive-mobile.md`), `produit/seo-indexabilite.md` et `contribution.md`.
