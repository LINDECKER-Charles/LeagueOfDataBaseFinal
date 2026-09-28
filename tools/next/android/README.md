# tools/next/android

Build tooling of the Android app (plan, L10.1; ADR 0007). The Capacitor project lives in
`src/LoDb.Web/android/` and `src/LoDb.Web/capacitor.config.ts`.

## Debug APK

```sh
tools/next/android/build-debug.sh
```

Builds `docker/next/android-build/` (JDK 21, Android SDK 36, Node 24, `linux/amd64`), then
builds a copy of the checkout inside it: `npm ci`, `build:shell:store`, `cap sync android`,
`gradlew assembleDebug`. The APK lands in `src/LoDb.Web/dist/android/lodb-debug.apk`.

- The container has its own `node_modules`; the host's never enters it.
- `cap sync` runs only in the container. On the host it would copy the bundle into
  `android/app/src/main/assets/public/`, which `prettier --check .` then reports.
- npm and Gradle downloads are kept in the volumes `lodb-android-npm` and
  `lodb-android-gradle`.
- A heavy task (about 6 GiB): never next to the legacy stack, another Android build or the E2E.

Gradle properties for a release build (L10.4): `-PlodbVersionCode`, `-PlodbVersionName`, and
`-PlodbAppLinkHost` (host of the App Link, `league-of-data-base.com` by default).

## Store variant

The app always embeds `shell-store` (`npm run build:shell:store`, `dist/shell-store/browser`):
the shell with `environment.payments === false`. Play only sells digital goods through its
own billing, so no payment screen may show there.

## assetlinks.json

```sh
node tools/next/android/assetlinks.mjs --fingerprint <SHA-256> [--fingerprint …] --out assetlinks.json
```

Writes the Digital Asset Links statement of `com.leagueofdatabase.app` for the given
certificate fingerprints (Play's app signing key, the upload key). nginx serves it at
`/.well-known/assetlinks.json` from `/etc/nginx/android/assetlinks.json`
(`docker/next/nginx/server.d/assetlinks.conf`); the served environment mounts that file.

The app declares the verified App Link `https://{lodbAppLinkHost}/app/oauth/…`: the OAuth
redirect URI of the Android app must start with that prefix.

## Tests

```sh
node --test tools/next/android/test/*.test.mjs
```
