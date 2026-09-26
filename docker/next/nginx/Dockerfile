# syntax=docker/dockerfile:1.7
# Image lodb-nginx: the stack's single HTTP entry point, behind the shared edge (Caddy).
# Build context = repository root; Dockerfile.dockerignore admits docker/next/nginx/ only.
#
# Layout, fixed for the later lots (they add files here without touching this Dockerfile):
#   nginx.conf  http block: upstreams, resolver, proxy_cache zone, shared maps
#   sites/      one file per server block, rendered by envsubst at start (LODB_* only)
#   server.d/   *.conf included INSIDE the site's server block (seo, legacy redirects…)
#   snippets/   files included inside a location (security headers, proxy headers…)
FROM nginx:1.31-alpine
ARG APP_REVISION=unknown
LABEL org.opencontainers.image.title="lodb-nginx" \
      org.opencontainers.image.source="https://github.com/LINDECKER-Charles/LeagueOfDataBaseFinal" \
      org.opencontainers.image.revision="${APP_REVISION}"

# The image's entrypoint renders /etc/nginx/templates/*.conf into /etc/nginx/sites/. The
# filter keeps envsubst away from nginx's own $variables. Defaults suit a local stack; a
# served environment sets all three (compose.next.deploy.yaml).
ENV APP_REVISION=${APP_REVISION} \
    NGINX_ENVSUBST_TEMPLATE_SUFFIX=.conf \
    NGINX_ENVSUBST_OUTPUT_DIR=/etc/nginx/sites \
    NGINX_ENVSUBST_FILTER=^LODB_ \
    LODB_CANONICAL_HOST=localhost \
    LODB_EDGE_CIDR=127.0.0.1/32 \
    LODB_NOINDEX=0

RUN rm -f /etc/nginx/conf.d/default.conf && mkdir -p /etc/nginx/sites
COPY docker/next/nginx/nginx.conf /etc/nginx/nginx.conf
COPY docker/next/nginx/sites/     /etc/nginx/templates/
COPY docker/next/nginx/server.d/  /etc/nginx/server.d/
COPY docker/next/nginx/snippets/  /etc/nginx/snippets/
