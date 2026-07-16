#!/bin/sh
set -e

# Writable runtime dirs (safe if they already exist)
mkdir -p var/cache var/log

# Warm the cache for immutable prod images (dev relies on live files)
if [ "${APP_ENV:-prod}" = "prod" ]; then
    php bin/console cache:warmup --no-interaction || true
fi

# Delegate to the official php entrypoint (handles php-fpm and friends)
exec docker-php-entrypoint "$@"
