# syntax=docker/dockerfile:1.7
# Build context = repository root.
# Produces the immutable Symfony php-fpm application image (source + vendor + built assets).

########################################################################
# Stage 1 — PHP base: runtime, extensions and config (shared)
########################################################################
FROM php:8.4-fpm-alpine AS php_base

# Reliable multi-extension installer (intl/icu, opcache, zip, ...).
# gd (with libwebp) powers WebP transcoding of DDragon images at ingestion.
# apcu backs the dev cache pool (config/packages/cache.yaml -> when@dev); it is
# harmless in prod, where ddragon.cache stays on the filesystem/Redis.
COPY --from=mlocati/php-extension-installer:2 /usr/bin/install-php-extensions /usr/local/bin/
RUN install-php-extensions intl opcache zip gd apcu

# Application PHP configuration
COPY docker/php/php.ini      /usr/local/etc/php/conf.d/zz-app.ini
COPY docker/php/php-fpm.conf /usr/local/etc/php-fpm.d/zz-app.conf

WORKDIR /var/www/html
ENV COMPOSER_ALLOW_SUPERUSER=1

########################################################################
# Stage 1b — Dev image: php_base + composer. Source is bind-mounted and
# vendor/ + var/ are named volumes (compose.override.yaml), so composer must run
# in-container to populate the vendor volume. composer lives ONLY here — the prod
# `app` image builds from php_base and never ships it. See docs/docker.md.
########################################################################
FROM php_base AS php_dev
COPY --from=composer:2 /usr/bin/composer /usr/bin/composer

########################################################################
# Stage 2 — Composer dependencies (production, no dev)
########################################################################
FROM php_base AS vendor
COPY --from=composer:2 /usr/bin/composer /usr/bin/composer

# Install deps first for optimal layer caching (no scripts, no autoloader yet)
COPY app/composer.json app/composer.lock app/symfony.lock ./
RUN composer install --no-dev --no-scripts --no-autoloader --prefer-dist --no-progress

# Bring in the source, then build an optimized authoritative autoloader
COPY app/ ./
RUN composer dump-autoload --no-dev --optimize --classmap-authoritative

########################################################################
# Stage 3 — Frontend assets (Webpack Encore today; swapped to Vite in Étape 2)
########################################################################
FROM node:22-alpine AS assets
WORKDIR /app
COPY app/package.json app/package-lock.json ./
RUN npm ci --no-audit --no-fund
COPY app/ ./
RUN npm run build

########################################################################
# Stage 4 — Final application image (php-fpm)
########################################################################
FROM php_base AS app
ENV APP_ENV=prod APP_DEBUG=0

# Application source (owned by the runtime user)
COPY --chown=www-data:www-data app/ ./
# Production vendor from the composer stage
COPY --from=vendor --chown=www-data:www-data /var/www/html/vendor ./vendor
# Compiled assets from the node stage
COPY --from=assets  --chown=www-data:www-data /app/public/build ./public/build

# A minimal .env must exist for symfony/dotenv (real secrets are injected at runtime)
RUN printf "APP_ENV=prod\nAPP_DEBUG=0\n" > .env

# Entrypoint prepares var/, warms cache, then runs php-fpm
COPY docker/php/docker-entrypoint.sh /usr/local/bin/docker-entrypoint
RUN chmod +x /usr/local/bin/docker-entrypoint \
 && mkdir -p var/cache var/log \
 && chown -R www-data:www-data var .env

USER www-data
EXPOSE 9000
ENTRYPOINT ["docker-entrypoint"]
CMD ["php-fpm"]
