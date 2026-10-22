# Changelog LeagueOfDataBase

Journal technique interne : source de vérité de tout ce qui a été livré, jamais filtré.

## Organisation

    docs/changelog/
      README.md          ← ce fichier
      TEMPLATE.md        ← template à dupliquer
      YYYY/              ← entrées non encore publiées (backlog prochaine release)
        YYYY-MM-DD-slug.md
      archived/          ← entrées déjà synthétisées dans une release publiée
        YYYY/
          YYYY-MM-DD-slug.md

Une entrée = un fichier. Pas de regroupement multi-changements dans un même fichier, même si livrés ensemble.

Une fois synthétisée dans une release publiée (voir archived/README.md), l'entrée est déplacée
dans archived/YYYY/ — la racine YYYY/ ne contient plus que le backlog de la prochaine release.

## Frontmatter

    ---
    date: YYYY-MM-DD        # date de livraison
    type: feat              # feat | fix | perf | ui | devops
    scope: front            # front | back | apps | infra | full-stack
    title: Titre court orienté joueur (≤ 80 caractères)
    summary: Une phrase d'impact côté joueur.
    tags: []                # optionnel
    ---

## Types

| Type      | Quand l'utiliser                                                |
|-----------|-----------------------------------------------------------------|
| feat      | Nouvelle fonctionnalité visible                                 |
| fix       | Correction de bug impactant le joueur                           |
| perf      | Gain de perf perceptible (chargement, fluidité)                 |
| ui        | Refonte ou amélioration UI / UX significative                   |
| devops    | Changement infra visible (disponibilité, déploiement, sécurité) |

## Scope

| Scope      | Périmètre                                                      |
|------------|----------------------------------------------------------------|
| front      | Angular : pages, SSR, SEO, i18n, design Hextech, PWA           |
| back       | API .NET : modules, ingestion Data Dragon, stockage, analytics |
| apps       | Desktop (Photino, Velopack) et Android (Capacitor, live update)|
| infra      | Docker, nginx, CI/CD, Caddy, volumes, configuration            |
| full-stack | Changement traversant plusieurs couches                        |

## Règles de rédaction

- Public visé : joueur, pas développeur. Pas de noms de classes, pas de stack-traces, pas de PR#.
- Phrases courtes, voix active.
- Si du contexte technique est utile, l'isoler en fin sous `## Technique`.
- Pas d'emoji dans le frontmatter ni le titre.

## Entrée remplacée avant la release

Quand un changement remplace le comportement d'une entrée encore dans `YYYY/`, l'ancienne
entrée reçoit, juste sous son frontmatter, une note qui dit ce qui ne vaut plus :

    > **Remplacé le AAAA-MM-JJ** par [`slug`](AAAA-MM-JJ-slug.md) : ce qui ne vaut plus.

La synthèse de la release publie le dernier état et ne reprend jamais ce qu'une note déclare
remplacé.

## Workflow

### Quotidien (journal technique)

1. Implémenter le changement.
2. Dupliquer TEMPLATE.md dans YYYY/YYYY-MM-DD-slug.md (jamais directement dans archived/).
3. Remplir frontmatter + corps.
4. Commit code + entrée changelog ensemble.

### Avant chaque release publique

1. Synthétiser le contenu de YYYY/ en une release du changelog public : un fichier
   `<id>.json` dans `src/LoDb.Web/src/app/features/editorial/changelog/published/`, que la
   page `/changelog` lit, et sa ligne dans `manifest.json` du même dossier (scope de commit
   `changelog`). Les entrées marquées « Remplacé » ne passent que pour leur dernier état.
2. Déplacer toutes les entrées synthétisées vers archived/YYYY/.
3. YYYY/ redevient le slot vide pour la prochaine release.
