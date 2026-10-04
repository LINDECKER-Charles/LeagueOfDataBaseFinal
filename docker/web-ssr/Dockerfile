# syntax=docker/dockerfile:1.7
# Image lodb-web-ssr: the Angular SSR server (build `web`, AngularNodeAppEngine on Express).
# Build context = repository root; Dockerfile.dockerignore admits the Angular workspace only.

# The bundles are plain JavaScript: building on the build machine avoids emulating Node.
FROM --platform=$BUILDPLATFORM node:24 AS build
WORKDIR /source/src/LoDb.Web
COPY src/LoDb.Web/package.json src/LoDb.Web/package-lock.json ./
RUN --mount=type=cache,id=lodb-npm,target=/root/.npm \
    npm ci --no-audit --no-fund
COPY src/LoDb.Web/ ./
# The build fails rather than ship a site without its releases (chip, footer, changelog page).
RUN npm run build:web && test -f dist/web/browser/changelog/manifest.json

# The server bundle inlines its dependencies: the runtime needs no node_modules.
FROM node:24-slim
ARG APP_REVISION=unknown
LABEL org.opencontainers.image.title="lodb-web-ssr" \
      org.opencontainers.image.source="https://github.com/LINDECKER-Charles/LeagueOfDataBaseFinal" \
      org.opencontainers.image.revision="${APP_REVISION}"
ENV NODE_ENV=production \
    PORT=4000 \
    APP_REVISION=${APP_REVISION}
WORKDIR /app
# Owned by root and read-only for `node`: a compromised renderer cannot rewrite the app.
COPY --from=build /source/src/LoDb.Web/dist/web ./
USER node
EXPOSE 4000
CMD ["node", "server/server.mjs"]
