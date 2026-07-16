# League of Database 🎮📊

[![License: CC BY-NC 4.0](https://img.shields.io/badge/License-CC%20BY--NC%204.0-lightgrey.svg)](https://creativecommons.org/licenses/by-nc/4.0/)
[![Symfony](https://img.shields.io/badge/Symfony-7.3-blue.svg)](https://symfony.com/)
[![PHP](https://img.shields.io/badge/PHP-8.4-purple.svg)](https://php.net/)
[![Go](https://img.shields.io/badge/Go-1.25-00ADD8.svg)](https://go.dev/)
[![Vue](https://img.shields.io/badge/Vue-3_+_TS-42b883.svg)](https://vuejs.org/)
[![Tailwind CSS](https://img.shields.io/badge/Tailwind_CSS-4-38B2AC.svg)](https://tailwindcss.com/)
[![Docker](https://img.shields.io/badge/Docker-Compose-2496ED.svg)](https://www.docker.com/)

> **A modern web application for accessing League of Legends data across all versions and languages**

---

## 🌍 Languages / Langues

- [🇫🇷 Français](#-français) (Default)
- [🇬🇧 English](#-english)
- [🇪🇸 Español](#-español)

---

## 🇫🇷 Français

### 📖 Aperçu

**League of Database** est une application web moderne développée avec **Symfony 7** qui permet d'accéder facilement aux données de **League of Legends** pour chaque version et dans toutes les langues disponibles. Le projet propose un système de mise en cache optimisé, une interface responsive, et une architecture modulaire pensée pour être extensible.

### ✨ Fonctionnalités principales

- 🔍 **Consultation complète** : Champions, Items, Sorts d'invocateur, Runes
- 🌐 **Multilingue** : Support de toutes les langues disponibles dans League of Legends
- 📱 **Interface responsive** : Design moderne avec Tailwind CSS
- ⚡ **Performance optimisée** : Passerelle Go + stockage MinIO adressé par contenu (déduplication SHA-256)
- 🔄 **Mise à jour automatique** : Synchronisation avec les données officielles de Riot Games
- 🎯 **Recherche avancée** : API de recherche en temps réel
- 📊 **Pagination intelligente** : Navigation fluide dans les grandes listes

### 🛠️ Stack technique

- **Backend** : Symfony 7.3 (PHP 8.4), sans base de données (proxy sur le CDN Data Dragon)
- **Microservice** : passerelle de *fetch* en **Go 1.25** (accès sortant vers Data Dragon, allowlist SSRF, batch parallèle)
- **Frontend** : Twig + **Vue 3 (TypeScript)** + **Vite** + **PrimeVue**, Tailwind CSS 4 (îlots Vue montés dans les coques Twig)
- **Stockage** : **MinIO** (compatible S3) — images adressées par contenu, déduplication SHA-256
- **API** : intégration Data Dragon de Riot Games
- **DevOps** : Docker Compose, GitHub Actions → images GHCR
- **Tests** : PHPUnit, `go test`, Playwright (captures)

### 🚀 Démarrage rapide (Docker)

> Prérequis : **Docker** + **Docker Compose**. Node/PHP ne sont nécessaires sur l'hôte que pour la préparation du bind-mount de dev (vendor + build des assets).

```bash
# 1. Cloner le projet
git clone https://github.com/LINDECKER-Charles/LeagueOfDataBaseFinal.git
cd LeagueOfDataBaseFinal

# 2. Configurer l'environnement (secrets CI : docs/github-actions-secrets.md)
cp .env.example .env

# 3. Pré-requis dev (le conteneur php monte ./app en bind-mount)
cd app && composer install && npm ci && npm run build && cd ..

# 4. Lancer toute la stack (php-fpm, nginx, Go, MinIO, Mailpit)
docker compose up -d --build
```

Services exposés en dev :

| Service | URL |
| --- | --- |
| Application | http://localhost:8080 |
| Console MinIO | http://localhost:9001 |
| Mailpit (mails) | http://localhost:8025 |
| Passerelle Go | http://localhost:8085/healthz |

Développement du front avec HMR (optionnel) : `cd app && npm run dev`.
Captures d'écran Playwright : `node tools/screenshots/capture.mjs` → dossier [`screenshot/`](screenshot/).

### 📚 Documentation

- [📋 Guide d'installation](docs/setup.md)
- [🏗️ Architecture du projet](docs/architecture.md)
- [🤝 Guide de contribution](docs/contribution.md)
- [🔧 Configuration](docs/configuration.md)

### 📄 Licence

**Creative Commons Attribution-NonCommercial 4.0 International (CC BY-NC 4.0)**

---

## 🇬🇧 English

### 📖 Overview

**League of Database** is a modern web application built with **Symfony 7** that provides easy access to **League of Legends** data across all versions and available languages. The project features an optimized caching system, responsive interface, and modular architecture designed for extensibility.

### ✨ Key Features

- 🔍 **Complete Data Access** : Champions, Items, Summoner Spells, Runes
- 🌐 **Multilingual** : Support for all League of Legends available languages
- 📱 **Responsive Interface** : Modern design with Tailwind CSS
- ⚡ **Optimized Performance** : Go fetch gateway + content-addressed MinIO storage (SHA-256 dedup)
- 🔄 **Auto Updates** : Synchronization with official Riot Games data
- 🎯 **Advanced Search** : Real-time search API
- 📊 **Smart Pagination** : Smooth navigation through large lists

### 🛠️ Tech Stack

- **Backend** : Symfony 7.3 (PHP 8.4), database-less (proxy over the Data Dragon CDN)
- **Microservice** : **Go 1.25** fetch gateway (outbound Data Dragon access, SSRF allowlist, parallel batching)
- **Frontend** : Twig + **Vue 3 (TypeScript)** + **Vite** + **PrimeVue**, Tailwind CSS 4 (Vue islands mounted into Twig shells)
- **Storage** : **MinIO** (S3-compatible) — content-addressed images, SHA-256 deduplication
- **API** : Riot Games Data Dragon integration
- **DevOps** : Docker Compose, GitHub Actions → GHCR images
- **Tests** : PHPUnit, `go test`, Playwright (screenshots)

### 🚀 Quick Start (Docker)

> Requirements: **Docker** + **Docker Compose**. Node/PHP on the host are only needed to prepare the dev bind-mount (vendor + asset build).

```bash
# 1. Clone the project
git clone https://github.com/LINDECKER-Charles/LeagueOfDataBaseFinal.git
cd LeagueOfDataBaseFinal

# 2. Configure the environment (CI secrets: docs/github-actions-secrets.md)
cp .env.example .env

# 3. Dev prerequisite (the php container bind-mounts ./app)
cd app && composer install && npm ci && npm run build && cd ..

# 4. Start the whole stack (php-fpm, nginx, Go, MinIO, Mailpit)
docker compose up -d --build
```

Services exposed in dev:

| Service | URL |
| --- | --- |
| Application | http://localhost:8080 |
| MinIO console | http://localhost:9001 |
| Mailpit (emails) | http://localhost:8025 |
| Go gateway | http://localhost:8085/healthz |

Frontend dev with HMR (optional): `cd app && npm run dev`.
Playwright screenshots: `node tools/screenshots/capture.mjs` → [`screenshot/`](screenshot/) folder.

### 📚 Documentation

- [📋 Setup Guide](docs/setup.md)
- [🏗️ Project Architecture](docs/architecture.md)
- [🤝 Contribution Guide](docs/contribution.md)
- [🔧 Configuration](docs/configuration.md)

### 📄 License

**Creative Commons Attribution-NonCommercial 4.0 International (CC BY-NC 4.0)**

---

## 🇪🇸 Español

### 📖 Resumen

**League of Database** es una aplicación web moderna desarrollada con **Symfony 7** que proporciona acceso fácil a los datos de **League of Legends** en todas las versiones e idiomas disponibles. El proyecto incluye un sistema de caché optimizado, interfaz responsiva y arquitectura modular diseñada para ser extensible.

### ✨ Características principales

- 🔍 **Acceso completo a datos** : Campeones, Objetos, Hechizos de invocador, Runas
- 🌐 **Multilingüe** : Soporte para todos los idiomas disponibles en League of Legends
- 📱 **Interfaz responsiva** : Diseño moderno con Tailwind CSS
- ⚡ **Rendimiento optimizado** : Pasarela Go + almacenamiento MinIO direccionado por contenido (dedup SHA-256)
- 🔄 **Actualizaciones automáticas** : Sincronización con datos oficiales de Riot Games
- 🎯 **Búsqueda avanzada** : API de búsqueda en tiempo real
- 📊 **Paginación inteligente** : Navegación fluida en listas grandes

### 🛠️ Stack técnico

- **Backend** : Symfony 7.3 (PHP 8.4), sin base de datos (proxy sobre el CDN Data Dragon)
- **Microservicio** : pasarela de *fetch* en **Go 1.25** (acceso saliente a Data Dragon, allowlist SSRF, batch paralelo)
- **Frontend** : Twig + **Vue 3 (TypeScript)** + **Vite** + **PrimeVue**, Tailwind CSS 4 (islas Vue montadas en las carcasas Twig)
- **Almacenamiento** : **MinIO** (compatible S3) — imágenes direccionadas por contenido, deduplicación SHA-256
- **API** : integración con Data Dragon de Riot Games
- **DevOps** : Docker Compose, GitHub Actions → imágenes GHCR
- **Tests** : PHPUnit, `go test`, Playwright (capturas)

### 🚀 Inicio rápido (Docker)

> Requisitos: **Docker** + **Docker Compose**. Node/PHP en el host solo se necesitan para preparar el bind-mount de desarrollo (vendor + build de assets).

```bash
# 1. Clonar el proyecto
git clone https://github.com/LINDECKER-Charles/LeagueOfDataBaseFinal.git
cd LeagueOfDataBaseFinal

# 2. Configurar el entorno (secretos CI: docs/github-actions-secrets.md)
cp .env.example .env

# 3. Requisito de desarrollo (el contenedor php monta ./app)
cd app && composer install && npm ci && npm run build && cd ..

# 4. Iniciar toda la stack (php-fpm, nginx, Go, MinIO, Mailpit)
docker compose up -d --build
```

Servicios expuestos en desarrollo:

| Servicio | URL |
| --- | --- |
| Aplicación | http://localhost:8080 |
| Consola MinIO | http://localhost:9001 |
| Mailpit (correos) | http://localhost:8025 |
| Pasarela Go | http://localhost:8085/healthz |

Desarrollo del frontend con HMR (opcional): `cd app && npm run dev`.
Capturas Playwright: `node tools/screenshots/capture.mjs` → carpeta [`screenshot/`](screenshot/).

### 📚 Documentación

- [📋 Guía de instalación](docs/setup.md)
- [🏗️ Arquitectura del proyecto](docs/architecture.md)
- [🤝 Guía de contribución](docs/contribution.md)
- [🔧 Configuración](docs/configuration.md)

### 📄 Licencia

**Creative Commons Attribution-NonCommercial 4.0 International (CC BY-NC 4.0)**

---

## 🤝 Contributing / Contribuer / Contribuir

We welcome contributions! Please see our [Contribution Guide](docs/contribution.md) for details.

Les contributions sont les bienvenues ! Consultez notre [Guide de contribution](docs/contribution.md) pour plus de détails.

¡Las contribuciones son bienvenidas! Consulta nuestra [Guía de contribución](docs/contribution.md) para más detalles.

---

## 📞 Support / Soutien / Soporte

- 📧 **Email** : [charles.lindecker@outlook.fr](mailto:charles.lindecker@outlook.fr)
- 🐛 **Issues** : [GitHub Issues](https://github.com/LINDECKER-Charles/LeagueOfDataBaseFinal/issues)


---

<div align="center">

**Made with ❤️ by the League of Database team**

[⭐ Star this repo](https://github.com/LINDECKER-Charles/LeagueOfDataBaseFinal) | [🐛 Report Bug](https://github.com/LINDECKER-Charles/LeagueOfDataBaseFinal/issues) | [💡 Request Feature](https://github.com/LINDECKER-Charles/LeagueOfDataBaseFinal/issues)

</div>