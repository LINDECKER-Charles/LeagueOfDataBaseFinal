# tools/android-release

Release tooling of the Android app (plan, L10.4; ADR 0008), run by
`.github/workflows/release-android.yml`. The guide `docs/guides/release-android.md` covers
the keys, the secrets and the publication.

| Folder or file         | Role                                                                    |
| ---------------------- | ----------------------------------------------------------------------- |
| `build/`               | version of the release, native baseline, bundles, embedded config check |
| `sign-release.sh`      | signs the AAB (upload key) and the APK (app signing key), then verifies |
| `gate/`                | update gate on an emulator, before anything is published                |
| `publish/`             | pre-release of the bundles, `latest.json`, promotion                    |
| `verify-local.sh`      | the release signature, locally, with throwaway keystores                |
| `native-baseline.json` | minimum native version of the bundles, with its fingerprint             |

## Version

```sh
node tools/android-release/build/release-version.mjs --sha <40 hex>
```

The tag `android-vX.Y.Z` on the commit gives the version; the versionCode is
`X*1000000 + Y*1000 + Z` (minor and patch below 1000, at most 2 100 000 000 as Play allows).
Prints `release=`, `version=`, `version_code=`, `tag=`, or `release=false` with a `reason=`.

## Native baseline

```sh
node tools/android-release/build/native-fingerprint.mjs --check X.Y.Z
node tools/android-release/build/native-fingerprint.mjs --update X.Y.Z
```

The fingerprint covers the committed files of `src/LoDb.Web/android/` and
`capacitor.config.ts` (`git ls-files -s`), plus the name and version of each plugin that
`cap sync` put in `capacitor.settings.gradle`. `--check` prints `minimum_native=`, the minimum
of the baseline, and fails once the native layer changed. The release that changes it runs
`--update` with its own version, and commits the baseline with the change: its bundles then
only reach the shells of that version and later.

## Bundles

```sh
LODB_LIVE_UPDATE_PRIVATE_KEY="$(cat /secure/lodb-live-update.pem)" \
  tools/android-release/build/bundles.sh --web src/LoDb.Web/dist/shell-store/browser \
    --version X.Y.Z --minimum-native A.B.C \
    --base-url https://github.com/<owner>/<repo>/releases/download/android-vX.Y.Z --out bundles
```

Signs `lodb-bundle-X.Y.Z.zip` and writes its descriptor, the values of
`client-policy publish --bundle-*` (`tools/live-update`). Also writes in `gate/` the two
bundles of the gate: a faulty one, signed but whose page never starts the app, and a
tampered one, the release zip plus a file under the release signature.

`check-embedded-config.mjs --apk <apk>` then reads `assets/capacitor.config.json` in the APK:
the `LiveUpdate` plugin must hold the public key of `LODB_LIVE_UPDATE_PUBLIC_KEY` (never a
private one) and a positive `readyTimeout`. Prints `ready_timeout=`.

## Signature

```sh
LODB_UPLOAD_KEYSTORE=… LODB_UPLOAD_KEY_ALIAS=… LODB_UPLOAD_STORE_PASSWORD=… \
LODB_UPLOAD_KEY_PASSWORD=… LODB_APK_KEYSTORE=… LODB_APK_KEY_ALIAS=… \
LODB_APK_STORE_PASSWORD=… LODB_APK_KEY_PASSWORD=… \
  tools/android-release/sign-release.sh --aab app-release.aab \
    --apk app-release-unsigned.apk --version X.Y.Z --out release
```

- AAB: `jarsigner` with the upload key, `jarsigner -verify`, then the certificate of the AAB
  compared with the keystore's (SHA-256).
- APK: `zipalign -p 4`, `apksigner sign` with the APK key (the app signing key of the
  transitional channel), `zipalign -c`, `apksigner verify` (scheme v2), same comparison.
- Writes `lodb-X.Y.Z.aab`, `lodb-X.Y.Z.apk` and `signing.txt` (`upload_sha256=`,
  `apk_sha256=`). The passwords go through the environment, never a command line.

Needs a JDK (`keytool`, `jarsigner`) and the Android build tools (`ANDROID_HOME`, or
`ANDROID_BUILD_TOOLS_DIR`).

## Local verification of the signature

```sh
tools/android-release/verify-local.sh [--version X.Y.Z]   # 1.0.0 by default
```

Builds `docker/android-build` (`lodb-android-build:local`), then in the container
(`linux/amd64`, 6 GiB, 6 CPUs, the checkout mounted read-only) runs
`lib/release-in-container.sh`:

1. copies the checkout, with the excludes of `tools/android/lib/build-in-container.sh`;
2. `npm ci`, `build:shell:store`, `cap sync android` with a throwaway bundle public key;
3. `gradlew bundleRelease assembleRelease` with the versionCode of the version;
4. generates two throwaway PKCS12 keystores (RSA 4096, valid one day, `CN=LoDb throwaway …`);
5. signs through `sign-release.sh`, the script of the workflow, then prints the certificates
   and the `package:` line of the APK (`aapt2 dump badging`).

The signed files land in `src/LoDb.Web/dist/android-release/`; never publish them. No real
key is read: the keystores and the bundle key die with the container. A heavy task: run it
alone, never next to the legacy stack, another Android build or the E2E.

## Gate

```sh
node tools/android-release/gate/gate.mjs --release-apk release/lodb-X.Y.Z.apk \
  --debug-apk app-debug.apk --bundles bundles --version X.Y.Z --ready-timeout <ms>
```

On a booted emulator (`adb` on the `PATH`), with the bundles staged at their URLs:

1. the signed release APK installs, starts and still runs 10 s later;
2. the debug APK, same front and `LiveUpdate` settings, starts on its embedded bundle; its
   WebView is open to DevTools, through which the gate drives the plugin
   (`Capacitor.nativePromise`, `gate/lib/remote-live-update.mjs`);
3. the tampered bundle is refused, as its signature does not hold;
4. the faulty bundle runs at the next cold start, is rolled back to the embedded bundle after
   the ready timeout, then removed;
5. the release bundle runs at the next cold start, and still runs after the ready timeout.

The emulator stays offline except while the gate downloads: the app's own checks then read
no client policy and move no bundle behind the gate's back.

## Publication

```sh
tools/android-release/publish/stage.sh --version X.Y.Z --repo <owner>/<repo> --bundles bundles
tools/android-release/publish/publish.sh --version X.Y.Z --repo <owner>/<repo> \
  --release release --transitional true|false
```

- `stage.sh` creates the pre-release `android-vX.Y.Z` on its tag (never the repository's
  latest release: that one is the desktop's feed), uploads the bundles and checks the release
  bundle downloaded back from its URL. A re-run reuses its own pre-release, never a published
  one.
- `publish.sh`, with the transitional channel on, uploads the APK and
  `lodb-android-latest.json` (`publish/latest-manifest.mjs`), downloads the APK back and checks
  its SHA-256; then promotes the release.

## Tests

```sh
node --test tools/android-release/test/*.test.mjs
```

`bundles.test.mjs` needs `zip` and `unzip`, and signs with a key generated for the run. The
gate, the signature and the publication only run in the workflow, or `verify-local.sh` for
the signature.
