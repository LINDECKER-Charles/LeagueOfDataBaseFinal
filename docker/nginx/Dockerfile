# syntax=docker/dockerfile:1.7
# Image lodb-nginx: the stack's single HTTP entry point, behind the shared edge (Caddy).
# Build context = repository root; Dockerfile.dockerignore admits docker/nginx/ only.
#
# Layout, fixed for the later lots (they add files here without touching this Dockerfile):
#   nginx.conf  http block: upstreams, resolver, shared maps
#   sites/      one file per server block; site.conf also holds the http-level bounds and
#               cache zones that LODB_* variables tune
#   server.d/   *.conf included INSIDE the site's server block (seo, legacy redirects…)
#   snippets/   files included by a server block or a location (guards, headers, caches…)
FROM nginx:1.31-alpine
ARG APP_REVISION=unknown
LABEL org.opencontainers.image.title="lodb-nginx" \
      org.opencontainers.image.source="https://github.com/LINDECKER-Charles/LeagueOfDataBaseFinal" \
      org.opencontainers.image.revision="${APP_REVISION}"

# The image's entrypoint renders /etc/nginx/templates/<folder>/<name>.template into
# /etc/nginx/<folder>/<name> at start. sites/, server.d/ and snippets/ are all templates, so
# a bound written inside a location (a burst) is tunable like one at http level. The filter
# keeps envsubst away from nginx's own $variables.
#
# Defaults suit a local stack; a served environment sets the first three
# (compose.deploy.yaml). LODB_REVISION, first part of the page cache's key, defaults to the
# image's revision (compose.yaml sets it); LODB_ARCHIVE_EPOCH, first part of the past
# patches' cache key, is raised by hand (docs/guides/configuration.md). The others bound each
# client and the caches (sites/site.conf): a rate in requests per second or minute (r/s,
# r/m), a burst in requests admitted at once past the rate, sizes in nginx units (k, m, g),
# durations in s, m, h, d.
# An empty value makes nginx refuse its configuration, and so does a variable that a
# container names without a value: Docker then drops the default below. compose.yaml
# therefore passes each bound with this same default (tools/nginx checks that they agree).
ENV APP_REVISION=${APP_REVISION} \
    NGINX_ENVSUBST_TEMPLATE_SUFFIX=.template \
    NGINX_ENVSUBST_OUTPUT_DIR=/etc/nginx \
    NGINX_ENVSUBST_FILTER=^LODB_ \
    LODB_CANONICAL_HOST=localhost \
    LODB_EDGE_CIDR=127.0.0.1/32 \
    LODB_NOINDEX=0 \
    LODB_REVISION=${APP_REVISION} \
    LODB_PAGES_RATE=20r/s \
    LODB_PAGES_BURST=100 \
    LODB_API_READS_RATE=10r/s \
    LODB_API_READS_BURST=40 \
    LODB_LEGACY_RATE=2r/s \
    LODB_LEGACY_BURST=10 \
    LODB_SITEMAPS_RATE=1r/s \
    LODB_SITEMAPS_BURST=20 \
    LODB_READYZ_RATE=1r/s \
    LODB_READYZ_BURST=5 \
    LODB_RATE_LIMITED_RETRY_AFTER=10 \
    LODB_MAX_BODY_SIZE=128k \
    LODB_WEBHOOK_MAX_BODY_SIZE=1m \
    LODB_PAGE_CACHE_SIZE=2g \
    LODB_PAGE_CACHE_KEYS_SIZE=32m \
    LODB_PAGE_CACHE_INACTIVE=24h \
    LODB_ASSETS_CACHE_SIZE=1g \
    LODB_ASSETS_CACHE_KEYS_SIZE=8m \
    LODB_ASSETS_CACHE_INACTIVE=30d \
    LODB_ARCHIVE_EPOCH=1 \
    LODB_ARCHIVE_CACHE_SIZE=6g \
    LODB_ARCHIVE_CACHE_KEYS_SIZE=32m \
    LODB_ARCHIVE_CACHE_INACTIVE=7d \
    LODB_ARCHIVE_CACHE_MIN_USES=2

# /var/cache/nginx/{pages,archive,assets} hold the caches of sites/site.conf; created here so
# that the volumes mounted on them (compose.yaml) start owned by the workers' user.
RUN rm -f /etc/nginx/conf.d/default.conf \
 && mkdir -p /var/cache/nginx/pages /var/cache/nginx/archive /var/cache/nginx/assets \
 && chown nginx:nginx /var/cache/nginx/pages /var/cache/nginx/archive /var/cache/nginx/assets
COPY docker/nginx/nginx.conf /etc/nginx/nginx.conf
COPY docker/nginx/sites/     /etc/nginx/templates/sites/
COPY docker/nginx/server.d/  /etc/nginx/templates/server.d/
COPY docker/nginx/snippets/  /etc/nginx/templates/snippets/
# The entrypoint strips the template suffix: each file keeps its .conf name once rendered.
RUN find /etc/nginx/templates -type f -name '*.conf' \
      -exec sh -c 'for file; do mv "$file" "$file.template"; done' sh '{}' +
