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
# `published` is a git symlink to app/public/changelog/, but a checkout without symlinks
# (Windows, core.symlinks=false) holds a text file or a junction to a host path there, which
# resolves nowhere in this image. The link is laid again, relative as git stores it, and the
# build fails rather than ship a site without its releases (chip, footer, changelog page).
RUN rm -rf src/app/features/editorial/changelog/published \
 && ln -s ../../../../../../../app/public/changelog src/app/features/editorial/changelog/published
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
