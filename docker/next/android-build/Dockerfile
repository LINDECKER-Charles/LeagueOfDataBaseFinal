# syntax=docker/dockerfile:1.7
# Image lodb-android-build: the toolchain of the Android app's build (L10.1): JDK 21,
# Android SDK 36 and Node 24. It holds no source: tools/next/android/build-debug.sh mounts
# the repository read-only and builds a copy inside the container.
# Build context = this folder (nothing else is needed).
#
# linux/amd64 only: aapt2, which the Android Gradle plugin downloads, has no Linux arm64
# build. On an Apple silicon host it runs under Docker Desktop's Rosetta emulation.
FROM --platform=linux/amd64 node:24-bookworm-slim AS node

FROM --platform=linux/amd64 eclipse-temurin:21-jdk-noble

# Command-line tools 23.0; SHA-1 from Google's repository manifest (repository2-3.xml).
ARG ANDROID_CMDLINE_TOOLS=16111833
ARG ANDROID_CMDLINE_TOOLS_SHA1=e025545c62a8e64c7559119566a569fb1dec5f60
# compileSdk and targetSdk of android/variables.gradle; build tools of the Android Gradle
# plugin 8.13 (android/build.gradle) and platform tools, so that Gradle downloads none.
ARG ANDROID_PLATFORM=android-36
ARG ANDROID_BUILD_TOOLS=35.0.0

LABEL org.opencontainers.image.title="lodb-android-build" \
      org.opencontainers.image.source="https://github.com/LINDECKER-Charles/LeagueOfDataBaseFinal"

ENV ANDROID_HOME=/opt/android-sdk \
    ANDROID_SDK_ROOT=/opt/android-sdk \
    GRADLE_USER_HOME=/cache/gradle \
    npm_config_cache=/cache/npm \
    npm_config_update_notifier=false
ENV PATH=${ANDROID_HOME}/cmdline-tools/latest/bin:${PATH}

RUN apt-get update \
 && apt-get install -y --no-install-recommends ca-certificates curl unzip \
 && rm -rf /var/lib/apt/lists/*

RUN curl -fsSL -o /tmp/cmdline-tools.zip \
        "https://dl.google.com/android/repository/commandlinetools-linux-${ANDROID_CMDLINE_TOOLS}_latest.zip" \
 && echo "${ANDROID_CMDLINE_TOOLS_SHA1}  /tmp/cmdline-tools.zip" | sha1sum -c - \
 && mkdir -p "${ANDROID_HOME}/cmdline-tools" \
 && unzip -q /tmp/cmdline-tools.zip -d "${ANDROID_HOME}/cmdline-tools" \
 && mv "${ANDROID_HOME}/cmdline-tools/cmdline-tools" "${ANDROID_HOME}/cmdline-tools/latest" \
 && rm /tmp/cmdline-tools.zip

RUN yes | sdkmanager --licenses > /dev/null \
 && sdkmanager --install platform-tools "platforms;${ANDROID_PLATFORM}" "build-tools;${ANDROID_BUILD_TOOLS}" > /dev/null

# Node from the official image: same build as the web-ssr image's.
COPY --from=node /usr/local/bin/node /usr/local/bin/node
COPY --from=node /usr/local/lib/node_modules/npm /usr/local/lib/node_modules/npm
RUN ln -s ../lib/node_modules/npm/bin/npm-cli.js /usr/local/bin/npm \
 && ln -s ../lib/node_modules/npm/bin/npx-cli.js /usr/local/bin/npx \
 && node --version && npm --version && java -version

WORKDIR /work
