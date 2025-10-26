# 🔧 Configuration

### ⚙️ Configuration de l'application

#### Variables d'environnement principales

```env
# Environnement
APP_ENV=dev                    # dev, test, prod
APP_DEBUG=true                 # true/false
APP_SECRET=your-secret-key     # Clé secrète pour les sessions

```

### 🎨 Configuration frontend

#### Tailwind CSS

```css
/* assets/styles/app.css */
@import "tailwindcss";

@theme {
  --color-lol-gold: #C9AA71;
  --color-lol-blue: #0F1423;
  --color-lol-red: #C89B3C;
  
  --font-family-beaufort: 'BeaufortForLoL', serif;
  --font-family-spiegel: 'Spiegel', sans-serif;
}

/* Utilisation des couleurs personnalisées */
.lol-gold {
  color: var(--color-lol-gold);
}

.lol-blue {
  color: var(--color-lol-blue);
}

.lol-red {
  color: var(--color-lol-red);
}
```

#### Webpack Encore

```javascript
// webpack.config.js
const Encore = require('@symfony/webpack-encore');

Encore
    .setOutputPath('public/build/')
    .setPublicPath('/build')
    .addEntry('app', './assets/app.js')
    .enableStimulusBridge('./assets/controllers.json')
    .enableSassLoader()
    .enablePostCssLoader()
    .enableSingleRuntimeChunk()
    .cleanupOutputBeforeBuild()
    .enableSourceMaps(!Encore.isProduction())
    .enableVersioning(Encore.isProduction())
    .configureBabel((config) => {
        config.plugins.push('@babel/plugin-proposal-class-properties');
    })
    .configureBabelPresetEnv((config) => {
        config.useBuiltIns = 'usage';
        config.corejs = 3;
    });

module.exports = Encore.getWebpackConfig();
```

### 🌐 Configuration multilingue

#### Support des langues

```yaml
# config/packages/translation.yaml
framework:
    default_locale: fr
    translator:
        default_path: '%kernel.project_dir%/translations'
        fallbacks:
            - fr
            - en
```

#### Fichiers de traduction

```yaml
# translations/messages.fr.yaml
app:
    title: "League of Database"
    description: "Base de données League of Legends"
    champions:
        title: "Champions"
        search: "Rechercher un champion"
    items:
        title: "Objets"
        search: "Rechercher un objet"
```

```yaml
# translations/messages.en.yaml
app:
    title: "League of Database"
    description: "League of Legends database"
    champions:
        title: "Champions"
        search: "Search for a champion"
    items:
        title: "Items"
        search: "Search for an item"
```

#### Configuration des permissions

```bash
# Script de configuration des permissions
#!/bin/bash
# setup-permissions.sh

# Créer les répertoires nécessaires
mkdir -p public/upload/{champions,items,summoners,runes}

# Définir les permissions
chmod -R 755 public/upload/
chmod -R 755 var/cache/
chmod -R 755 var/log/

# Propriétaire (ajuster selon votre configuration)
chown -R www-data:www-data public/upload/
chown -R www-data:www-data var/
```
---
