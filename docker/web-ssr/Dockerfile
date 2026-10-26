# syntax=docker/dockerfile:1.7@sha256:a57df69d0ea827fb7266491f2813635de6f17269be881f696fbfdf2d83dda33e
# Image lodb-web-ssr: the Angular SSR server (build `web`, AngularNodeAppEngine on Express).
# Build context = repository root; Dockerfile.dockerignore admits the Angular workspace only.
#
# Every image this file names is pinned by digest, its tag kept for the reader: a moved tag
# never changes what is built. Dependabot bumps both (tools/ci/README.md, base images).

# The bundles are plain JavaScript: building on the build machine avoids emulating Node.
FROM --platform=$BUILDPLATFORM node:24@sha256:6449e19d8184d84062525b6a7524def03146c2cb04a7f08b076680d0e8e4240a AS build
WORKDIR /source/src/LoDb.Web
COPY src/LoDb.Web/package.json src/LoDb.Web/package-lock.json ./
RUN --mount=type=cache,id=lodb-npm,target=/root/.npm \
    npm ci --no-audit --no-fund
COPY src/LoDb.Web/ ./
# The build fails rather than ship a site without its releases (chip, footer, changelog page).
RUN npm run build:web && test -f dist/web/browser/changelog/manifest.json

# The server bundle inlines its dependencies: the runtime needs no node_modules.
FROM node:24-slim@sha256:55fb5f5a527ea2c5c4a95f963e48ef90aa38c7b346054fdc8ee9957a2b55d205 AS runtime
# The distribution's security fixes published since the base image was built: _build.yml
# rebuilds this stage without its cache, so that each build takes those of the day, before its
# scan. Then Node's package managers go: the server runs `node` alone, and npm, corepack and
# yarn would only hand a compromised renderer more tools, and ship advisories of their own.
RUN apt-get update \
 && DEBIAN_FRONTEND=noninteractive apt-get upgrade -y \
 && rm -rf /var/lib/apt/lists/* \
 && rm -rf /usr/local/lib/node_modules /opt/yarn-v* /usr/local/bin/npm /usr/local/bin/npx \
      /usr/local/bin/corepack /usr/local/bin/yarn /usr/local/bin/yarnpkg
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
# Mount point of the bundles every build shares (volume `front-builds`, compose.yaml): created
# here, owned by `node`, so that a new volume starts writable for the bundles each start adds.
# The renderer's only writable place that outlives a deploy: nginx keeps its files from
# running when opened directly, and docs/guides/configuration.md says how to empty it.
RUN mkdir -p /srv/front-builds && chown node:node /srv/front-builds
USER node
# 4000 for nginx; 9464 for the host's metrics collector only, never published.
EXPOSE 4000 9464
CMD ["node", "server/server.mjs"]
