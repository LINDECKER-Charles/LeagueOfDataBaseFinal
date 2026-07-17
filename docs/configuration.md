# 🔧 Configuration

### ⚙️ Configuration de l'application

#### Variables d'environnement principales

```env
# Environnement
APP_ENV=dev                    # dev, test, prod
APP_DEBUG=true                 # true/false
APP_SECRET=your-secret-key     # Clé secrète pour les sessions

```

### 🗄️ Base de données & nouvelles variables

PostgreSQL 17 porte **uniquement les données utilisateur** (comptes, favoris, builds) ;
les données et images Data Dragon restent sur MinIO. Défauts du compose de dev :

```env
# PostgreSQL — service `postgres` du compose (défauts dev, à surcharger hors dev)
POSTGRES_USER=lodb
POSTGRES_PASSWORD=lodb
POSTGRES_DB=lodb
DATABASE_URL="postgresql://lodb:lodb@postgres:5432/lodb?serverVersion=17&charset=utf8"

# Stripe — page de don /donate + webhook /webhooks/stripe
# Placeholders : renseigner depuis le dashboard Stripe (cf. docs/legal-info.md).
# Vides ⇒ la page de don annonce proprement que la passerelle est désactivée.
STRIPE_SECRET_KEY=             # sk_test_… en dev, sk_live_… en production
STRIPE_WEBHOOK_SECRET=         # whsec_… (signature des webhooks Stripe)
```

Le conteneur `php` reçoit `DATABASE_URL` assemblée depuis `POSTGRES_*` (voir `compose.yaml`) ;
ne surcharger `DATABASE_URL` que pour pointer une base externe. Migrations :
`docker compose exec -T php php bin/console doctrine:migrations:migrate`.

En production/staging, ces valeurs vivent dans les secrets GitHub Actions
(`ENV_PROD` / `ENV_STAGING`) — voir `docs/github-actions-secrets.md`.

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

Deux niveaux de langue cohabitent :

- **Contenu Data Dragon** (`fr_FR`, `en_US`, `es_ES`…) : la locale des données Riot,
  choisie par l'utilisateur dans le setup, persistée en session + cookie signé `lod_prefs`.
- **Interface (traductions Symfony)** : le texte du site (`trans`). Elle **suit désormais
  la langue Data Dragon sélectionnée** ; le TLD (`.fr` → `fr`, sinon `en`) ne sert plus que
  de défaut tant qu'aucune langue n'a été choisie.

#### Résolution de la locale d'interface

`LocaleSubscriber` (priorité 20, avant le `LocaleListener` de Symfony) lit la locale DDragon
choisie via `ClientManager::getSelectedLocale()` (session, sinon cookie `lod_prefs` — sans
démarrer de session), puis `UiLocaleResolver` la mappe vers une locale d'UI :

- collapse vers la base 2 lettres (`fr_FR` → `fr`, `en_AU` → `en`, `es_MX` → `es`, `pt_BR` → `pt`) ;
- le chinois conserve la distinction d'écriture (`zh_CN`/`zh_MY` → `zh_Hans`, `zh_TW` → `zh_Hant`) ;
- fallback vers le défaut de domaine si la langue n'a pas de catalogue.

Les catalogues embarqués sont déclarés dans `framework.enabled_locales` et couvrent toutes les
langues de l'API : `ar cs de el en es fr hu id it ja ko pl pt ro ru th tr vi zh_Hans zh_Hant`.

```yaml
# config/packages/framework.yaml (extrait)
framework:
    default_locale: en
    enabled_locales: [ar, cs, de, el, en, es, fr, hu, id, it, ja, ko, pl, pt, ro, ru, th, tr, vi, zh_Hans, zh_Hant]
    translator:
        default_path: '%kernel.project_dir%/translations'
        fallbacks: [en]
```

#### Fichiers de traduction

Un catalogue `translations/messages.<locale>.yaml` par langue, de structure identique (mêmes clés,
placeholders `%version%`/`%name%`/`%locale%` et balises HTML préservés) :

```yaml
# translations/messages.en.yaml (extrait)
homepage:
    title: "League of Data Base"
item:
    list:
        header: "Items"
        search_placeholder: "Search for an item…"
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
