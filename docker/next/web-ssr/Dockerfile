# syntax=docker/dockerfile:1.7
# Image lodb-web-ssr: the Angular SSR server (build `web`, AngularNodeAppEngine on Express).
# Build context = repository root, so that later lots can feed the build from outside the
# workspace (app/public/changelog/, L3.10); Dockerfile.dockerignore admits only that.

# The bundles are plain JavaScript: building on the build machine avoids emulating Node.
FROM --platform=$BUILDPLATFORM node:24 AS build
WORKDIR /source/src/LoDb.Web
COPY src/LoDb.Web/package.json src/LoDb.Web/package-lock.json ./
RUN --mount=type=cache,id=lodb-npm,target=/root/.npm \
    npm ci --no-audit --no-fund
COPY app/public/changelog/ /source/app/public/changelog/
COPY src/LoDb.Web/ ./
RUN npm run build:web

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
