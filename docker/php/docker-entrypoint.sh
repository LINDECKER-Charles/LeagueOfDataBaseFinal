#!/bin/sh
set -e

# Writable runtime dirs (safe if they already exist). var/state is volume-backed
# and survives container recreation; everything else under var/ is disposable.
mkdir -p var/cache var/log var/state

# Warm the cache for immutable prod images (dev relies on live files)
if [ "${APP_ENV:-prod}" = "prod" ]; then
    php bin/console cache:warmup --no-interaction || true
fi

# Delegate to the official php entrypoint (handles php-fpm and friends)
exec docker-php-entrypoint "$@"
