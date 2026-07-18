# syntax=docker/dockerfile:1.7
# Build context = repository root.
# Bakes the compiled public/ from the app image into an nginx image (immutable prod).
ARG APP_IMAGE=lodb/app:latest
FROM ${APP_IMAGE} AS app

FROM nginx:1.27-alpine
COPY docker/nginx/nginx.conf   /etc/nginx/nginx.conf
COPY docker/nginx/snippets     /etc/nginx/snippets
COPY docker/nginx/default.conf /etc/nginx/conf.d/default.conf
COPY --from=app /var/www/html/public /var/www/html/public
