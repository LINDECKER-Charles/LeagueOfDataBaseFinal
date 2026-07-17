# 🤝 Guide de contribution

> 📌 **Guide court & canonique :** [`CONTRIBUTING.md`](../CONTRIBUTING.md) · **Conventions de code complètes :** [`CLAUDE.md`](../CLAUDE.md).
> Ce document en est la version détaillée et multilingue (onboarding, templates d'issue et de PR). En cas de divergence, `CONTRIBUTING.md` et `CLAUDE.md` font foi.

## 🌍 Languages / Langues

- [🇫🇷 Français](#français) (Default)
- [🇬🇧 English](#english)
- [🇪🇸 Español](#español)

---

## 🇫🇷 Français

### 🎯 Comment contribuer

Nous sommes ravis que vous souhaitiez contribuer à **League of Database** ! Ce guide vous explique comment participer efficacement au projet.

### 📋 Prérequis

- **Docker** + **Docker Compose** — la stack complète (PHP 8.4 / Symfony 7.4, micro-service Go, MinIO) tourne en conteneurs. Rien à installer en local côté backend.
- **Node.js 20+** et **npm** — pour le développement front et les garde-fous (hors conteneur, depuis `app/`).
- **Git** pour le contrôle de version.
- Connaissance de base de **Symfony 7 / Twig** et des îlots **Vue 3 / TypeScript**.

### 🚀 Configuration initiale

1. **Fork du projet**, puis clonez votre fork :
   ```bash
   git clone https://github.com/VOTRE_USERNAME/LeagueOfDataBaseFinal.git
   cd LeagueOfDataBaseFinal

   # Ajouter le repository upstream
   git remote add upstream https://github.com/LINDECKER-Charles/LeagueOfDataBaseFinal.git
   ```

2. **Lancer la stack**
   ```bash
   docker compose up -d --build
   # app :8080 · MinIO console :9001 · Mailpit :8025 · go-fetcher :8085/healthz
   ```
   Détails : [`docker.md`](docker.md), [`configuration.md`](configuration.md).

3. **Dev front (optionnel, depuis `app/`)**
   ```bash
   cd app && npm install
   ```

### 🌿 Workflow Git

#### ⚠️ IMPORTANT : Branche de développement

**Toutes les Pull Requests doivent cibler la branche `dev`, PAS `main` !**

```bash
git checkout dev
git pull upstream dev

# Créer votre branche
git checkout -b feature/votre-fonctionnalite   # ou fix/… docs/… refactor/… test/…
```

#### Convention de nommage des branches

- `feature/nom-fonctionnalite` : Nouvelles fonctionnalités
- `fix/description-bug` : Corrections de bugs
- `docs/type-documentation` : Améliorations de la documentation
- `refactor/description-refactoring` : Refactoring de code
- `test/description-tests` : Ajout ou amélioration de tests

### 📝 Processus de contribution

#### 1. Développement & commits

Nous utilisons le format **Conventional Commits** : `type(scope): description`.

**Types :** `feat`, `fix`, `docs`, `style`, `refactor`, `test`, `chore`. Le scope reflète la zone touchée (`front`, `back`, `i18n`, `champion`, `docker`…).

```bash
git commit -m "feat(champion): recherche par rôle"
git commit -m "fix(loader): course manifeste read-merge-write"
git commit -m "docs(api): mise à jour de la documentation des endpoints"
```

#### 2. Garde-fous (obligatoires avant PR)

À faire passer **au vert** avant toute PR (identique à la CI) :

```bash
# Backend — dans le conteneur
docker compose exec -T php php vendor/bin/phpunit tests/Unit

# Front — depuis app/
npm test          # vitest
npm run typecheck # vue-tsc --noEmit
npm run build     # vite build
```

> `tests/Functional/AdminAccessTest` échoue en conteneur `APP_ENV=dev` (`framework.test` inactif) — **pré-existant**, vert en CI. La baseline backend est `tests/Unit`.

#### 3. Push et Pull Request

```bash
git push origin feature/votre-fonctionnalite
```

**Créer une Pull Request :**
1. Aller sur GitHub → "Compare & pull request".
2. **IMPORTANT** : sélectionner `dev` comme branche de base (pas `main`).
3. Remplir le template de PR ci-dessous.

### 📋 Template de Pull Request

Rien à copier : ouvrir une PR pré-remplit automatiquement le [template natif](../.github/PULL_REQUEST_TEMPLATE.md).

### 🎨 Standards de code

> Les règles complètes vivent dans [`../CLAUDE.md`](../CLAUDE.md) (source unique). Résumé de ce qui bloque une revue :

#### PHP (Symfony)
- `declare(strict_types=1);` en tête de chaque fichier ; classes `final` par défaut (sauf base abstraite).
- Typage strict partout (propriétés, params, retours), `readonly` pour l'injection ; enums/DTO plutôt que tableaux associatifs ; exceptions typées.

#### TypeScript / Vue
- `<script setup lang="ts">`, pas de `any` — `vue-tsc --noEmit` doit passer.
- Orchestration en composables (`useXxx`) + helpers purs ; le SFC reste présentation.
- Réutiliser le design system (`app.css`, variables `--color-*`, `--font-*`) — pas de couleurs/typo en dur.

#### Twig
- Logique métier hors des templates.

#### Limites (plafonds)
- Fichier ≤ 300 lignes (500 max) · fonction ≤ 30 lignes · ≤ 4 paramètres · imbrication ≤ 3 · complexité ≤ 10 · ligne ≤ 120.
- Un seul élément public par fichier, nommé comme le fichier. Pas de nombres/chaînes magiques.
- Commentaires **en anglais**, expliquant le **pourquoi** (pas le *quoi*).

### 🐛 Signaler un bug · 💡 Proposer une fonctionnalité

Le tracker propose des **formulaires guidés** (bug / fonctionnalité) : voir [`.github/ISSUE_TEMPLATE`](../.github/ISSUE_TEMPLATE). Rien à copier-coller.

### 🔍 Review process

- **Contributeurs** : auto-review, garde-fous verts, documentation à jour, impact perf vérifié.
- **Reviewers** : qualité & conformité aux conventions, couverture de tests, préservation des invariants et du comportement, impact perf.

---

## 🇬🇧 English

### 🎯 How to Contribute

We're excited that you want to contribute to **League of Database**! This guide explains how to participate effectively in the project.

### 📋 Prerequisites

- **Docker** + **Docker Compose** — the full stack (PHP 8.4 / Symfony 7.4, Go micro-service, MinIO) runs in containers. Nothing to install locally for the backend.
- **Node.js 20+** and **npm** — for frontend development and guardrails (outside the container, from `app/`).
- **Git** for version control.
- Basic knowledge of **Symfony 7 / Twig** and **Vue 3 / TypeScript** islands.

### 🚀 Initial Setup

1. **Fork the project**, then clone your fork:
   ```bash
   git clone https://github.com/YOUR_USERNAME/LeagueOfDataBaseFinal.git
   cd LeagueOfDataBaseFinal

   # Add upstream repository
   git remote add upstream https://github.com/LINDECKER-Charles/LeagueOfDataBaseFinal.git
   ```

2. **Start the stack**
   ```bash
   docker compose up -d --build
   # app :8080 · MinIO console :9001 · Mailpit :8025 · go-fetcher :8085/healthz
   ```
   Details: [`docker.md`](docker.md), [`configuration.md`](configuration.md).

3. **Frontend dev (optional, from `app/`)**
   ```bash
   cd app && npm install
   ```

### 🌿 Git Workflow

#### ⚠️ IMPORTANT: Development Branch

**All Pull Requests must target the `dev` branch, NOT `main`!**

```bash
git checkout dev
git pull upstream dev

# Create your branch
git checkout -b feature/your-feature   # or fix/… docs/… refactor/… test/…
```

#### Branch naming convention

- `feature/feature-name` : New features
- `fix/bug-description` : Bug fixes
- `docs/documentation-type` : Documentation improvements
- `refactor/refactoring-description` : Code refactoring
- `test/test-description` : Adding or improving tests

### 📝 Contribution Process

#### 1. Development & commits

We use the **Conventional Commits** format: `type(scope): description`.

**Types:** `feat`, `fix`, `docs`, `style`, `refactor`, `test`, `chore`. The scope reflects the touched area (`front`, `back`, `i18n`, `champion`, `docker`…).

```bash
git commit -m "feat(champion): add role-based search"
git commit -m "fix(loader): manifest read-merge-write race"
git commit -m "docs(api): update endpoints documentation"
```

#### 2. Guardrails (required before PR)

Must be **green** before any PR (same as CI):

```bash
# Backend — inside the container
docker compose exec -T php php vendor/bin/phpunit tests/Unit

# Frontend — from app/
npm test          # vitest
npm run typecheck # vue-tsc --noEmit
npm run build     # vite build
```

> `tests/Functional/AdminAccessTest` fails in the `APP_ENV=dev` container (`framework.test` inactive) — **pre-existing**, green in CI. The backend baseline is `tests/Unit`.

#### 3. Push and Pull Request

```bash
git push origin feature/your-feature
```

**Create a Pull Request:**
1. Go to GitHub → "Compare & pull request".
2. **IMPORTANT**: select `dev` as the base branch (not `main`).
3. Fill in the PR template below.

### 📋 Pull Request Template

Nothing to copy: opening a PR automatically pre-fills the [native template](../.github/PULL_REQUEST_TEMPLATE.md).

### 🎨 Code Standards

> The full rules live in [`../CLAUDE.md`](../CLAUDE.md) (single source). Summary of what blocks a review:

#### PHP (Symfony)
- `declare(strict_types=1);` at the top of every file; classes `final` by default (except abstract bases).
- Strict typing everywhere (properties, params, returns), `readonly` for injection; enums/DTOs over associative arrays; typed exceptions.

#### TypeScript / Vue
- `<script setup lang="ts">`, no `any` — `vue-tsc --noEmit` must pass.
- Orchestration in composables (`useXxx`) + pure helpers; the SFC stays presentation.
- Reuse the design system (`app.css`, `--color-*`, `--font-*` variables) — no hard-coded colors/typography.

#### Twig
- Keep business logic out of templates.

#### Limits (ceilings)
- File ≤ 300 lines (500 max) · function ≤ 30 lines · ≤ 4 parameters · nesting ≤ 3 · complexity ≤ 10 · line ≤ 120.
- One public element per file, named after the file. No magic numbers/strings.
- Comments **in English**, explaining the **why** (not the *what*).

### 🐛 Reporting Bugs · 💡 Proposing Features

The tracker provides **guided forms** (bug / feature): see [`.github/ISSUE_TEMPLATE`](../.github/ISSUE_TEMPLATE). Nothing to copy-paste.

### 🔍 Review Process

- **Contributors:** self-review, green guardrails, updated documentation, verified performance impact.
- **Reviewers:** quality & conformance to conventions, test coverage, preservation of invariants and behavior, performance impact.

---

## 🇪🇸 Español

### 🎯 Cómo Contribuir

¡Estamos emocionados de que quieras contribuir a **League of Database**! Esta guía explica cómo participar efectivamente en el proyecto.

### 📋 Prerrequisitos

- **Docker** + **Docker Compose** — el stack completo (PHP 8.4 / Symfony 7.4, micro-servicio Go, MinIO) corre en contenedores. Nada que instalar localmente para el backend.
- **Node.js 20+** y **npm** — para el desarrollo frontend y los controles (fuera del contenedor, desde `app/`).
- **Git** para control de versiones.
- Conocimiento básico de **Symfony 7 / Twig** y de los islands **Vue 3 / TypeScript**.

### 🚀 Configuración Inicial

1. **Fork del proyecto**, luego clona tu fork:
   ```bash
   git clone https://github.com/TU_USUARIO/LeagueOfDataBaseFinal.git
   cd LeagueOfDataBaseFinal

   # Agregar repositorio upstream
   git remote add upstream https://github.com/LINDECKER-Charles/LeagueOfDataBaseFinal.git
   ```

2. **Levantar el stack**
   ```bash
   docker compose up -d --build
   # app :8080 · MinIO console :9001 · Mailpit :8025 · go-fetcher :8085/healthz
   ```
   Detalles: [`docker.md`](docker.md), [`configuration.md`](configuration.md).

3. **Dev frontend (opcional, desde `app/`)**
   ```bash
   cd app && npm install
   ```

### 🌿 Flujo de Trabajo Git

#### ⚠️ IMPORTANTE: Rama de Desarrollo

**¡Todas las Pull Requests deben apuntar a la rama `dev`, NO `main`!**

```bash
git checkout dev
git pull upstream dev

# Crear tu rama
git checkout -b feature/tu-funcionalidad   # o fix/… docs/… refactor/… test/…
```

#### Convención de nombres de ramas

- `feature/nombre-funcionalidad` : Nuevas funcionalidades
- `fix/descripcion-bug` : Correcciones de bugs
- `docs/tipo-documentacion` : Mejoras de documentación
- `refactor/descripcion-refactoring` : Refactoring de código
- `test/descripcion-tests` : Agregar o mejorar tests

### 📝 Proceso de Contribución

#### 1. Desarrollo y commits

Usamos el formato **Conventional Commits**: `type(scope): description`.

**Tipos:** `feat`, `fix`, `docs`, `style`, `refactor`, `test`, `chore`. El scope refleja la zona tocada (`front`, `back`, `i18n`, `champion`, `docker`…).

```bash
git commit -m "feat(champion): agregar búsqueda por rol"
git commit -m "fix(loader): carrera read-merge-write del manifiesto"
git commit -m "docs(api): actualizar documentación de endpoints"
```

#### 2. Controles (obligatorios antes de la PR)

Deben estar en **verde** antes de cualquier PR (igual que la CI):

```bash
# Backend — dentro del contenedor
docker compose exec -T php php vendor/bin/phpunit tests/Unit

# Frontend — desde app/
npm test          # vitest
npm run typecheck # vue-tsc --noEmit
npm run build     # vite build
```

> `tests/Functional/AdminAccessTest` falla en el contenedor `APP_ENV=dev` (`framework.test` inactivo) — **pre-existente**, verde en CI. La baseline del backend es `tests/Unit`.

#### 3. Push y Pull Request

```bash
git push origin feature/tu-funcionalidad
```

**Crear una Pull Request:**
1. Ir a GitHub → "Compare & pull request".
2. **IMPORTANTE**: seleccionar `dev` como rama base (no `main`).
3. Rellenar el template de PR de abajo.

### 📋 Template de Pull Request

Nada que copiar: abrir una PR pre-rellena automáticamente el [template nativo](../.github/PULL_REQUEST_TEMPLATE.md).

### 🎨 Estándares de Código

> Las reglas completas viven en [`../CLAUDE.md`](../CLAUDE.md) (fuente única). Resumen de lo que bloquea una revisión:

#### PHP (Symfony)
- `declare(strict_types=1);` al inicio de cada archivo; clases `final` por defecto (salvo bases abstractas).
- Tipado estricto en todas partes (propiedades, params, retornos), `readonly` para la inyección; enums/DTOs en vez de arrays asociativos; excepciones tipadas.

#### TypeScript / Vue
- `<script setup lang="ts">`, sin `any` — `vue-tsc --noEmit` debe pasar.
- Orquestación en composables (`useXxx`) + helpers puros; el SFC se queda en presentación.
- Reutilizar el design system (`app.css`, variables `--color-*`, `--font-*`) — sin colores/tipografía hardcodeados.

#### Twig
- Mantener la lógica de negocio fuera de los templates.

#### Límites (topes)
- Archivo ≤ 300 líneas (500 máx) · función ≤ 30 líneas · ≤ 4 parámetros · anidamiento ≤ 3 · complejidad ≤ 10 · línea ≤ 120.
- Un solo elemento público por archivo, nombrado como el archivo. Sin números/cadenas mágicas.
- Comentarios **en inglés**, explicando el **porqué** (no el *qué*).

### 🐛 Reportar Bugs · 💡 Proponer Funcionalidades

El tracker ofrece **formularios guiados** (bug / funcionalidad): ver [`.github/ISSUE_TEMPLATE`](../.github/ISSUE_TEMPLATE). Nada que copiar y pegar.

### 🔍 Proceso de Revisión

- **Contribuidores:** auto-revisión, controles en verde, documentación actualizada, impacto de rendimiento verificado.
- **Revisores:** calidad y conformidad con las convenciones, cobertura de tests, preservación de invariantes y comportamiento, impacto de rendimiento.
