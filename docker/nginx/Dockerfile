# syntax=docker/dockerfile:1.7
# Image lodb-nginx: the stack's single HTTP entry point, behind the shared edge (Caddy).
# Build context = repository root; Dockerfile.dockerignore admits docker/nginx/ only.
#
# Layout, fixed for the later lots (they add files here without touching this Dockerfile):
#   nginx.conf  http block: upstreams, resolver, proxy_cache zone, shared maps
#   sites/      one file per server block
#   server.d/   *.conf included INSIDE the site's server block (seo, legacy redirects…)
#   snippets/   files included inside a location (security headers, proxy headers…)
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
# image's revision (compose.yaml sets it). An empty value makes nginx refuse its
# configuration: leave a variable out to keep its default, never set it empty.
ENV APP_REVISION=${APP_REVISION} \
    NGINX_ENVSUBST_TEMPLATE_SUFFIX=.template \
    NGINX_ENVSUBST_OUTPUT_DIR=/etc/nginx \
    NGINX_ENVSUBST_FILTER=^LODB_ \
    LODB_CANONICAL_HOST=localhost \
    LODB_EDGE_CIDR=127.0.0.1/32 \
    LODB_NOINDEX=0 \
    LODB_REVISION=${APP_REVISION}

# /var/cache/nginx/pages holds the page cache (nginx.conf); created here so that the volume
# mounted on it (compose.yaml) starts owned by the workers' user.
RUN rm -f /etc/nginx/conf.d/default.conf \
 && mkdir -p /var/cache/nginx/pages \
 && chown nginx:nginx /var/cache/nginx/pages
COPY docker/nginx/nginx.conf /etc/nginx/nginx.conf
COPY docker/nginx/sites/     /etc/nginx/templates/sites/
COPY docker/nginx/server.d/  /etc/nginx/templates/server.d/
COPY docker/nginx/snippets/  /etc/nginx/templates/snippets/
# The entrypoint strips the template suffix: each file keeps its .conf name once rendered.
RUN find /etc/nginx/templates -type f -name '*.conf' \
      -exec sh -c 'for file; do mv "$file" "$file.template"; done' sh '{}' +
