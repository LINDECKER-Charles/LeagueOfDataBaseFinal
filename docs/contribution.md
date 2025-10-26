# 🤝 Guide de contribution

## 🌍 Languages / Langues

- [🇫🇷 Français](#français) (Default)
- [🇬🇧 English](#english)
- [🇪🇸 Español](#español)

---

## 🇫🇷 Français

### 🎯 Comment contribuer

Nous sommes ravis que vous souhaitiez contribuer à **League of Database** ! Ce guide vous explique comment participer efficacement au projet.

### 📋 Prérequis

- **PHP 8.2+** installé
- **Composer** pour la gestion des dépendances PHP
- **Node.js 18+** et **npm** pour les assets frontend
- **Git** pour le contrôle de version
- Connaissance de base de **Symfony 7** et **Twig**

### 🚀 Configuration initiale

1. **Fork du projet**
   ```bash
   # Fork le projet sur GitHub, puis clonez votre fork
   git clone https://github.com/VOTRE_USERNAME/LeagueOfDataBaseFinal.git
   cd LeagueOfDataBaseFinal/app
   ```

2. **Configuration de l'environnement**
   ```bash
   # Ajouter le repository upstream
   git remote add upstream https://github.com/LINDECKER-Charles/LeagueOfDataBaseFinal.git
   
   # Installer les dépendances
   composer install
   npm install
   ```


### 🌿 Workflow Git

#### ⚠️ IMPORTANT : Branche de développement

**Toutes les Pull Requests doivent être créées sur la branche `dev`, PAS sur `main` !**

```bash
# Toujours partir de la branche dev
git checkout dev
git pull upstream dev

# Créer votre branche de fonctionnalité
git checkout -b feature/votre-fonctionnalite
# ou
git checkout -b fix/correction-bug
# ou
git checkout -b docs/amelioration-documentation
```

#### Convention de nommage des branches

- `feature/nom-fonctionnalite` : Nouvelles fonctionnalités
- `fix/description-bug` : Corrections de bugs
- `docs/type-documentation` : Améliorations de la documentation
- `refactor/description-refactoring` : Refactoring de code
- `test/description-tests` : Ajout ou amélioration de tests

### 📝 Processus de contribution

#### 1. Développement

```bash
# Travailler sur votre branche
git checkout feature/votre-fonctionnalite

# Faire vos modifications
# ... code ...

# Commiter régulièrement
git add .
git commit -m "feat: ajout de la fonctionnalité X"
```

#### 2. Convention des commits

Nous utilisons le format **Conventional Commits** :

```
<type>(<scope>): <description>

[body optionnel]

[footer optionnel]
```

**Types acceptés :**
- `feat` : Nouvelle fonctionnalité
- `fix` : Correction de bug
- `docs` : Documentation
- `style` : Formatage, point-virgules manquants, etc.
- `refactor` : Refactoring de code
- `test` : Ajout ou modification de tests
- `chore` : Tâches de maintenance

**Exemples :**
```bash
git commit -m "feat(champion): ajout de la recherche par rôle"
git commit -m "fix(cache): correction du problème de hard links"
git commit -m "docs(api): mise à jour de la documentation des endpoints"
```

#### 3. Tests et validation

```bash
# Lancer tous les tests
./bin/phpunit

# Tests spécifiques
./bin/phpunit tests/Unit/Service/ChampionManagerTest.php

# Vérifier le code
composer run-script phpstan
composer run-script cs-fix
```

#### 4. Push et Pull Request

```bash
# Pousser votre branche
git push origin feature/votre-fonctionnalite
```

**Créer une Pull Request :**
1. Aller sur GitHub
2. Cliquer sur "Compare & pull request"
3. **IMPORTANT** : Sélectionner `dev` comme branche de base (pas `main`)
4. Remplir le template de PR

### 📋 Template de Pull Request

```markdown
## 📝 Description
Décrivez brièvement les changements apportés.

## 🔗 Type de changement
- [ ] Bug fix (changement non-breaking qui corrige un problème)
- [ ] Nouvelle fonctionnalité (changement non-breaking qui ajoute une fonctionnalité)
- [ ] Breaking change (fix ou fonctionnalité qui causerait un changement de comportement existant)
- [ ] Documentation (changement uniquement dans la documentation)

## 🧪 Tests
- [ ] Mes changements nécessitent des tests
- [ ] J'ai ajouté des tests qui prouvent que mes changements fonctionnent
- [ ] Tous les tests passent localement

## 📸 Captures d'écran (si applicable)
Ajoutez des captures d'écran pour expliquer vos changements.

## ✅ Checklist
- [ ] Mon code suit les conventions de style du projet
- [ ] J'ai effectué une auto-review de mon code
- [ ] J'ai commenté mon code, particulièrement dans les zones difficiles à comprendre
- [ ] J'ai mis à jour la documentation correspondante
- [ ] Mes changements ne génèrent pas de nouveaux warnings
- [ ] J'ai ajouté des tests qui prouvent que mes changements fonctionnent
- [ ] Les tests passent avec mes changements
- [ ] Les changements sont compatibles avec les versions existantes
```

### 🎨 Standards de code

#### PHP (Symfony)
- Respecter les standards PSR-12
- Utiliser le typage strict (`declare(strict_types=1);`)
- Documenter toutes les méthodes publiques avec PHPDoc
- Utiliser des noms de variables et méthodes explicites

#### Twig
- Utiliser l'indentation de 4 espaces
- Préférer les filtres Twig aux fonctions PHP dans les templates
- Séparer la logique métier des templates

#### JavaScript/CSS
- Utiliser ESLint et Prettier pour le JavaScript
- Suivre les conventions Tailwind CSS
- Préférer les composants Stimulus réutilisables

### 🐛 Signaler un bug

Utilisez le template d'issue pour les bugs :

```markdown
## 🐛 Description du bug
Une description claire et concise du problème.

## 🔄 Étapes pour reproduire
1. Aller à '...'
2. Cliquer sur '...'
3. Voir l'erreur

## ✅ Comportement attendu
Ce qui devrait se passer.

## 📸 Captures d'écran
Si applicable, ajoutez des captures d'écran.

## 🖥️ Environnement
- OS: [ex. Windows 10]
- PHP: [ex. 8.2.0]
- Symfony: [ex. 7.3.0]
- Navigateur: [ex. Chrome 120]

## 📋 Informations supplémentaires
Toute autre information pertinente.
```

### 💡 Proposer une fonctionnalité

```markdown
## 💡 Description de la fonctionnalité
Une description claire et concise de la fonctionnalité souhaitée.

## 🎯 Problème résolu
Quel problème cette fonctionnalité résout-elle ?

## 💭 Solution proposée
Décrivez la solution que vous aimeriez voir implémentée.

## 🔄 Alternatives considérées
Décrivez les solutions alternatives que vous avez considérées.

## 📋 Informations supplémentaires
Toute autre information pertinente.
```

### 🔍 Review process

#### Pour les contributeurs
1. **Auto-review** : Relisez votre code avant de soumettre
2. **Tests** : Assurez-vous que tous les tests passent
3. **Documentation** : Mettez à jour la documentation si nécessaire
4. **Performance** : Vérifiez l'impact sur les performances

#### Pour les reviewers
1. **Code review** : Vérifier la qualité et la conformité
2. **Tests** : S'assurer que les tests couvrent les changements
3. **Documentation** : Vérifier la mise à jour de la documentation
4. **Performance** : Évaluer l'impact sur les performances

---

## 🇬🇧 English

### 🎯 How to Contribute

We're excited that you want to contribute to **League of Database**! This guide explains how to participate effectively in the project.

### 📋 Prerequisites

- **PHP 8.2+** installed
- **Composer** for PHP dependency management
- **Node.js 18+** and **npm** for frontend assets
- **Git** for version control
- Basic knowledge of **Symfony 7** and **Twig**

### 🚀 Initial Setup

1. **Fork the project**
   ```bash
   # Fork the project on GitHub, then clone your fork
   git clone https://github.com/VOTRE_USERNAME/LeagueOfDataBaseFinal.git
   cd LeagueOfDataBaseFinal/app
   ```

2. **Environment configuration**
   ```bash
   # Add upstream repository
   git remote add upstream https://github.com/LINDECKER-Charles/LeagueOfDataBaseFinal.git
   
   # Install dependencies
   composer install
   npm install
   ```

3. **Verify setup**
   ```bash
   # Run tests
   ./bin/phpunit
   
   # Check linting
   composer run-script phpstan
   ```

### 🌿 Git Workflow

#### ⚠️ IMPORTANT: Development Branch

**All Pull Requests must be created against the `dev` branch, NOT `main`!**

```bash
# Always start from dev branch
git checkout dev
git pull upstream dev

# Create your feature branch
git checkout -b feature/your-feature
# or
git checkout -b fix/bug-description
# or
git checkout -b docs/documentation-improvement
```

#### Branch naming convention

- `feature/feature-name` : New features
- `fix/bug-description` : Bug fixes
- `docs/documentation-type` : Documentation improvements
- `refactor/refactoring-description` : Code refactoring
- `test/test-description` : Adding or improving tests

### 📝 Contribution Process

#### 1. Development

```bash
# Work on your branch
git checkout feature/your-feature

# Make your changes
# ... code ...

# Commit regularly
git add .
git commit -m "feat: add feature X"
```

#### 2. Commit Convention

We use **Conventional Commits** format:

```
<type>(<scope>): <description>

[optional body]

[optional footer]
```

**Accepted types:**
- `feat` : New feature
- `fix` : Bug fix
- `docs` : Documentation
- `style` : Formatting, missing semicolons, etc.
- `refactor` : Code refactoring
- `test` : Adding or modifying tests
- `chore` : Maintenance tasks

**Examples:**
```bash
git commit -m "feat(champion): add role-based search"
git commit -m "fix(cache): fix hard links issue"
git commit -m "docs(api): update endpoints documentation"
```

#### 3. Tests and Validation

```bash
# Run all tests
./bin/phpunit

# Specific tests
./bin/phpunit tests/Unit/Service/ChampionManagerTest.php

# Check code
composer run-script phpstan
composer run-script cs-fix
```

#### 4. Push and Pull Request

```bash
# Push your branch
git push origin feature/your-feature
```

**Create a Pull Request:**
1. Go to GitHub
2. Click "Compare & pull request"
3. **IMPORTANT**: Select `dev` as base branch (not `main`)
4. Fill the PR template

### 📋 Pull Request Template

```markdown
## 📝 Description
Briefly describe the changes made.

## 🔗 Type of change
- [ ] Bug fix (non-breaking change which fixes an issue)
- [ ] New feature (non-breaking change which adds functionality)
- [ ] Breaking change (fix or feature that would cause existing functionality to not work as expected)
- [ ] Documentation (changes only in documentation)

## 🧪 Tests
- [ ] My changes require tests
- [ ] I have added tests that prove my changes work
- [ ] All tests pass locally

## 📸 Screenshots (if applicable)
Add screenshots to explain your changes.

## ✅ Checklist
- [ ] My code follows the project's style conventions
- [ ] I have performed a self-review of my code
- [ ] I have commented my code, particularly in hard-to-understand areas
- [ ] I have updated the corresponding documentation
- [ ] My changes generate no new warnings
- [ ] I have added tests that prove my changes work
- [ ] Tests pass with my changes
- [ ] Changes are compatible with existing versions
```

### 🎨 Code Standards

#### PHP (Symfony)
- Follow PSR-12 standards
- Use strict typing (`declare(strict_types=1);`)
- Document all public methods with PHPDoc
- Use explicit variable and method names

#### Twig
- Use 4-space indentation
- Prefer Twig filters over PHP functions in templates
- Separate business logic from templates

#### JavaScript/CSS
- Use ESLint and Prettier for JavaScript
- Follow Tailwind CSS conventions
- Prefer reusable Stimulus components

### 🐛 Reporting Bugs

Use the bug issue template:

```markdown
## 🐛 Bug Description
A clear and concise description of the problem.

## 🔄 Steps to Reproduce
1. Go to '...'
2. Click on '...'
3. See error

## ✅ Expected Behavior
What should happen.

## 📸 Screenshots
If applicable, add screenshots.

## 🖥️ Environment
- OS: [e.g. Windows 10]
- PHP: [e.g. 8.2.0]
- Symfony: [e.g. 7.3.0]
- Browser: [e.g. Chrome 120]

## 📋 Additional Information
Any other relevant information.
```

### 💡 Proposing Features

```markdown
## 💡 Feature Description
A clear and concise description of the desired feature.

## 🎯 Problem Solved
What problem does this feature solve?

## 💭 Proposed Solution
Describe the solution you'd like to see implemented.

## 🔄 Alternatives Considered
Describe alternative solutions you've considered.

## 📋 Additional Information
Any other relevant information.
```

### 🔍 Review Process

#### For Contributors
1. **Self-review** : Review your code before submitting
2. **Tests** : Ensure all tests pass
3. **Documentation** : Update documentation if necessary
4. **Performance** : Check performance impact

#### For Reviewers
1. **Code review** : Check quality and compliance
2. **Tests** : Ensure tests cover changes
3. **Documentation** : Verify documentation updates
4. **Performance** : Evaluate performance impact

### 🏆 Recognition

All contributors are recognized in:
- The `CONTRIBUTORS.md` file
- Release notes
- Project documentation

---

## 🇪🇸 Español

### 🎯 Cómo Contribuir

¡Estamos emocionados de que quieras contribuir a **League of Database**! Esta guía explica cómo participar efectivamente en el proyecto.

### 📋 Prerrequisitos

- **PHP 8.2+** instalado
- **Composer** para gestión de dependencias PHP
- **Node.js 18+** y **npm** para assets frontend
- **Git** para control de versiones
- Conocimiento básico de **Symfony 7** y **Twig**

### 🚀 Configuración Inicial

1. **Fork del proyecto**
   ```bash
   # Fork el proyecto en GitHub, luego clona tu fork
   git clone https://github.com/VOTRE_USERNAME/LeagueOfDataBaseFinal.git
   cd LeagueOfDataBaseFinal/app
   ```

2. **Configuración del entorno**
   ```bash
   # Agregar repositorio upstream
   git remote add upstream https://github.com/LINDECKER-Charles/LeagueOfDataBaseFinal.git
   
   # Instalar dependencias
   composer install
   npm install
   ```

3. **Verificar configuración**
   ```bash
   # Ejecutar tests
   ./bin/phpunit
   
   # Verificar linting
   composer run-script phpstan
   ```

### 🌿 Flujo de Trabajo Git

#### ⚠️ IMPORTANTE: Rama de Desarrollo

**¡Todas las Pull Requests deben crearse contra la rama `dev`, NO `main`!**

```bash
# Siempre empezar desde la rama dev
git checkout dev
git pull upstream dev

# Crear tu rama de funcionalidad
git checkout -b feature/tu-funcionalidad
# o
git checkout -b fix/descripcion-bug
# o
git checkout -b docs/mejora-documentacion
```

#### Convención de nombres de ramas

- `feature/nombre-funcionalidad` : Nuevas funcionalidades
- `fix/descripcion-bug` : Correcciones de bugs
- `docs/tipo-documentacion` : Mejoras de documentación
- `refactor/descripcion-refactoring` : Refactoring de código
- `test/descripcion-tests` : Agregar o mejorar tests

### 📝 Proceso de Contribución

#### 1. Desarrollo

```bash
# Trabajar en tu rama
git checkout feature/tu-funcionalidad

# Hacer tus cambios
# ... código ...

# Commitear regularmente
git add .
git commit -m "feat: agregar funcionalidad X"
```

#### 2. Convención de Commits

Usamos el formato **Conventional Commits**:

```
<type>(<scope>): <description>

[cuerpo opcional]

[pie opcional]
```

**Tipos aceptados:**
- `feat` : Nueva funcionalidad
- `fix` : Corrección de bug
- `docs` : Documentación
- `style` : Formateo, punto y coma faltante, etc.
- `refactor` : Refactoring de código
- `test` : Agregar o modificar tests
- `chore` : Tareas de mantenimiento

**Ejemplos:**
```bash
git commit -m "feat(champion): agregar búsqueda por rol"
git commit -m "fix(cache): corregir problema de hard links"
git commit -m "docs(api): actualizar documentación de endpoints"
```

#### 3. Tests y Validación

```bash
# Ejecutar todos los tests
./bin/phpunit

# Tests específicos
./bin/phpunit tests/Unit/Service/ChampionManagerTest.php

# Verificar código
composer run-script phpstan
composer run-script cs-fix
```

#### 4. Push y Pull Request

```bash
# Pushear tu rama
git push origin feature/tu-funcionalidad
```

**Crear una Pull Request:**
1. Ir a GitHub
2. Hacer clic en "Compare & pull request"
3. **IMPORTANTE**: Seleccionar `dev` como rama base (no `main`)
4. Llenar el template de PR

### 📋 Template de Pull Request

```markdown
## 📝 Descripción
Describe brevemente los cambios realizados.

## 🔗 Tipo de cambio
- [ ] Corrección de bug (cambio no-breaking que corrige un problema)
- [ ] Nueva funcionalidad (cambio no-breaking que agrega funcionalidad)
- [ ] Cambio breaking (fix o funcionalidad que causaría cambio de comportamiento existente)
- [ ] Documentación (cambios solo en documentación)

## 🧪 Tests
- [ ] Mis cambios requieren tests
- [ ] He agregado tests que prueban que mis cambios funcionan
- [ ] Todos los tests pasan localmente

## 📸 Capturas de pantalla (si aplica)
Agrega capturas de pantalla para explicar tus cambios.

## ✅ Checklist
- [ ] Mi código sigue las convenciones de estilo del proyecto
- [ ] He realizado una auto-revisión de mi código
- [ ] He comentado mi código, especialmente en áreas difíciles de entender
- [ ] He actualizado la documentación correspondiente
- [ ] Mis cambios no generan nuevas advertencias
- [ ] He agregado tests que prueban que mis cambios funcionan
- [ ] Los tests pasan con mis cambios
- [ ] Los cambios son compatibles con versiones existantes
```

### 🎨 Estándares de Código

#### PHP (Symfony)
- Seguir estándares PSR-12
- Usar tipado estricto (`declare(strict_types=1);`)
- Documentar todos los métodos públicos con PHPDoc
- Usar nombres de variables y métodos explícitos

#### Twig
- Usar indentación de 4 espacios
- Preferir filtros Twig sobre funciones PHP en templates
- Separar lógica de negocio de templates

#### JavaScript/CSS
- Usar ESLint y Prettier para JavaScript
- Seguir convenciones Tailwind CSS
- Preferir componentes Stimulus reutilizables

### 🐛 Reportar Bugs

Usar el template de issue para bugs:

```markdown
## 🐛 Descripción del Bug
Una descripción clara y concisa del problema.

## 🔄 Pasos para Reproducir
1. Ir a '...'
2. Hacer clic en '...'
3. Ver el error

## ✅ Comportamiento Esperado
Lo que debería pasar.

## 📸 Capturas de Pantalla
Si aplica, agregar capturas de pantalla.

## 🖥️ Entorno
- OS: [ej. Windows 10]
- PHP: [ej. 8.2.0]
- Symfony: [ej. 7.3.0]
- Navegador: [ej. Chrome 120]

## 📋 Información Adicional
Cualquier otra información relevante.
```

### 💡 Proponer Funcionalidades

```markdown
## 💡 Descripción de la Funcionalidad
Una descripción clara y concisa de la funcionalidad deseada.

## 🎯 Problema Resuelto
¿Qué problema resuelve esta funcionalidad?

## 💭 Solución Propuesta
Describe la solución que te gustaría ver implementada.

## 🔄 Alternativas Consideradas
Describe soluciones alternativas que has considerado.

## 📋 Información Adicional
Cualquier otra información relevante.
```

### 🔍 Proceso de Revisión

#### Para Contribuidores
1. **Auto-revisión** : Revisa tu código antes de enviar
2. **Tests** : Asegúrate de que todos los tests pasen
3. **Documentación** : Actualiza la documentación si es necesario
4. **Rendimiento** : Verifica el impacto en el rendimiento

#### Para Revisores
1. **Revisión de código** : Verificar calidad y cumplimiento
2. **Tests** : Asegurar que los tests cubran los cambios
3. **Documentación** : Verificar actualizaciones de documentación
4. **Rendimiento** : Evaluar impacto en el rendimiento

### 🏆 Reconocimiento

Todos los contribuidores son reconocidos en:
- El archivo `CONTRIBUTORS.md`
- Notas de release
- Documentación del proyecto
