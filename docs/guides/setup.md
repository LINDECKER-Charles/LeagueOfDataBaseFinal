# 📋 Guide d'installation et configuration

### 📋 Prérequis système

#### Configuration minimale requise

- **PHP 8.2+** (recommandé : PHP 8.3)
- **Composer 2.6+**
- **Node.js 18+** (recommandé : Node.js 20+)
- **npm 9+** ou **yarn 1.22+**
- **Git 2.30+**
- **Symfony CLI** (optionnel mais recommandé)


#### Espace disque

- **Minimum** : 2 GB d'espace libre
- **Recommandé** : 5 GB+ (pour le cache des images et données)

### 🚀 Installation

#### 1. Cloner le projet

```bash
# Cloner le repository
git clone https://github.com/LINDECKER-Charles/LeagueOfDataBaseFinal.git
cd LeagueOfDataBaseFinal/app

# Ou si vous contribuez (fork)
git clone https://github.com/VOTRE_USERNAME/LeagueOfDataBaseFinal.git
cd LeagueOfDataBaseFinal/app
git remote add upstream https://github.com/LINDECKER-Charles/LeagueOfDataBaseFinal.git
```

#### 2. Installation des dépendances PHP

```bash
# Installer les dépendances Composer
composer install

# En mode développement (avec outils de dev)
composer install --dev
```

#### 3. Installation des dépendances frontend

```bash
# Installer les dépendances Node.js
npm install

# Ou avec yarn
yarn install
```

#### 4. Configuration de l'environnement

```bash
# Éditer le fichier .env selon vos besoins
nano .env
```

**Configuration minimale dans `.env` :**

```env
# Environnement
APP_ENV=dev
APP_SECRET=your-secret-key-here
```

#### 5. Initialisation du projet

```bash
# Créer les répertoires si nécessaires
mkdir -p var/cache var/log public/upload

# Définir les permissions (Linux/macOS)
chmod -R 755 var/
chmod -R 755 public/upload/

# Vider le cache
php bin/console cache:clear
```

#### 6. Compilation des assets

```bash
# Compilation en mode développement
npm run dev

# Ou avec yarn
yarn dev

# Compilation en mode production
npm run build
# ou
yarn build
```

### 🖥️ Lancement du serveur

#### Option 1 : Symfony CLI (recommandé)

```bash
# Installer Symfony CLI si pas déjà fait
curl -sS https://get.symfony.com/cli | bash

# Lancer le serveur
symfony serve -d

# L'application sera disponible sur http://127.0.0.1:8000
```

#### Option 2 : Serveur PHP intégré

```bash
# Dans le répertoire public/
cd public/
php -S 127.0.0.1:8000

# Ou depuis la racine du projet
php -S 127.0.0.1:8000 -t public/
```

#### Option 3 : Serveur de développement Webpack

```bash
# Terminal 1 : Serveur Symfony
symfony serve -d

# Terminal 2 : Serveur Webpack (pour le hot reload)
npm run watch
# ou
yarn dev-server
```

### 🔍 Dépannage

#### Problèmes courants

**1. Erreur de permissions**
```bash
# Solution
sudo chown -R $USER:$USER var/
sudo chmod -R 755 var/
sudo chmod -R 755 public/upload/
```

**2. Problème de mémoire PHP**
```bash
# Augmenter la limite de mémoire
php -d memory_limit=512M bin/console cache:clear
```

**3. Erreur de dépendances Composer**
```bash
# Nettoyer et réinstaller
rm -rf vendor/
composer clear-cache
composer install
```

**4. Problème avec les assets**
```bash 
# Nettoyer et rebuilder
rm -rf node_modules/
rm -rf public/build/
npm install
npm run build
```

**5. Cache corrompu**
```bash
# Vider tous les caches
php bin/console cache:clear --env=dev
php bin/console cache:clear --env=prod
rm -rf var/cache/*
```

#### Logs et debugging

```bash
# Voir les logs en temps réel
tail -f var/log/dev.log

# Activer le mode debug
# Dans .env
APP_ENV=dev
APP_DEBUG=true

# Voir les routes disponibles
php bin/console debug:router
```

### 📊 Monitoring et performance

#### Outils de monitoring

```bash
# Profiler Symfony (en mode dev)
# Accessible via http://127.0.0.1:8000/_profiler

# Monitoring des performances
php bin/console debug:container --show-arguments
php bin/console debug:router
```

#### Optimisations

```bash
# Optimiser l'autoloader Composer
composer dump-autoload --optimize

# Compiler les assets en production
npm run build

# Vider le cache Symfony
php bin/console cache:clear --env=prod
```