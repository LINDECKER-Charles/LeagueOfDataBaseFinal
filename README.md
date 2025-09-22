# League Of Data Base — README ✨📘🛠️

League Of Data Base est une application web développée avec **Symfony 7** qui permet d’accéder aux données de **League of Legends** pour chaque version et dans toutes les langues disponibles. Le projet propose un système de mise en cache optimisé, une interface responsive, ainsi qu’une architecture modulaire pensée pour être extensible par d’autres développeurs.

---

## 📖 Aperçu

- **Setup des préférences** (version/langue) : `/`  
- **Liste des Summoner Spells** (avec images locales si disponibles) : `/summoners`  
- **Cache disque** pour JSON + images (réduction des appels API)  
- **DTO `ClientData`** injecté dans les vues (versions, langues, labels, locale courante, session)

---

## 🚀 Fonctionnalités principales

- Consultation des données **League of Legends** par version et langue.  
- Mise en cache optimisée pour réduire les appels API et améliorer les performances.  
- Interface **responsive** réalisée en Twig + Tailwind CSS.  
- **Architecture collaborative** : managers extensibles, héritage & polymorphisme.  
- Gestion des **préférences utilisateurs** via sessions, cookies et paramètres d’URL (pour partage de pages).  
- Téléchargement local des images pour éviter la dépendance directe à l’API.  

---

## ⚙️ Architecture & services internes

- **Architecture collaborative extensible** : services modulaires, héritage et polymorphisme pour centraliser les fonctions génériques et imposer l’implémentation des méthodes spécifiques dans chaque manager.  
- **Optimisation du stockage** : utilisation de hard links pour ne jamais enregistrer deux fois la même image et réduire l’espace disque.  
- **ClientManager** : détection de la langue navigateur, gestion session/cookie, hydratation.  
- **VersionManager** : récupération et validation des versions/langues depuis DDragon (avec cache).  
- **SummonerManager** : parsing JSON, tri, téléchargement et cache des images de sorts.  
- **UploadManager** : gestion du stockage (JSON/images).  
- **APICaller** : service HTTP minimaliste pour récupérer les données externes.  
- **DTO `ClientData`** : centralisation des données envoyées aux vues (versions, langues, préférences utilisateur).  

---

## 🛠️ Stack technique

- **Backend** : Symfony 7 (PHP 8.3)  
- **Frontend** : Twig, Tailwind CSS  
- **DevOps** : Docker, GitHub Actions (CI/CD)  
- **Tests** : PHPUnit  

---

## 📦 Installation

### Prérequis
- PHP 8.3+  
- Composer  
- Node.js + npm (ou Yarn si Encore est utilisé)  

### Étapes
```bash
# Cloner le projet
git clone https://github.com/tonprofil/league-of-data-base.git
cd league-of-data-base

# Installer les dépendances PHP
composer install

# Installer les dépendances front
npm install && npm run build

```

## 🖥️ Utilisation

```bash
# Lancer le serveur Symfony
npm run --watch & symfony serve -d
# Puis ouvrez : http://127.0.0.1:8000/setup
```

1. Configurez vos préférences version/langue.  
2. Consultez les données (ex. : `/summoners`).  
3. Les JSON et images sont mis en cache automatiquement.  
4. Partagez vos pages via l’URL contenant les paramètres.  

---

## 🧪 Tests

```bash
composer require --dev symfony/phpunit-bridge
./bin/phpunit
```

---

## 🔄 CI (GitHub Actions)

- Installation PHP 8.3 + dépendances Composer.  
- Lint YAML/Twig/Container.  
- Lancement des tests PHPUnit.  
- (Option) Merge/push auto vers `main` si tests OK.  

---

## 📌 Roadmap

- [ ] Pages Champions / Objets / Runes.  
- [ ] Pré-chargement des images au build.  
- [ ] Filtrage par mode (ARAM/CLASSIC/URF…).  
- [ ] Tests d’intégration pour `SummonerManager`.  

---

## 🤝 Contribuer

1. Forkez le projet.  
2. Créez une branche (`feature/ma-fonctionnalite`).  
3. Commitez vos changements.  
4. Poussez la branche.  
5. Ouvrez une Pull Request.  

---

## 📄 Licence

**Creative Commons Attribution-NonCommercial 4.0 International (CC BY-NC 4.0)**  

Copyright (c) 2025 Charles — League of Data Base  

Vous êtes libre de :  
- **Partager** : copier et redistribuer le matériel sur tout support.  
- **Adapter** : remixer, transformer et créer à partir du matériel.  

Selon les conditions suivantes :  
- **Attribution** : vous devez créditer l’auteur et fournir un lien vers la licence.  
- **NonCommercial** : vous ne pouvez pas utiliser ce projet à des fins commerciales.  

Texte complet : [https://creativecommons.org/licenses/by-nc/4.0/](https://creativecommons.org/licenses/by-nc/4.0/)
