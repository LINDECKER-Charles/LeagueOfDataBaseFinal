// latest.json of the transitional channel (lib/apk-manifest.mjs):
//
//   node tools/next/android-release/publish/latest-manifest.mjs --apk lodb-X.Y.Z.apk \
//        --version X.Y.Z --url https://github.com/<repo>/releases/download/<tag>/lodb-X.Y.Z.apk \
//        --out lodb-android-latest.json
//   node tools/next/android-release/publish/latest-manifest.mjs --verify lodb-android-latest.json \
//        --apk downloaded.apk
//
// The first writes the manifest of the APK; the second checks an APK, downloaded back from
// the manifest's URL, against its SHA-256.
import { readFileSync, writeFileSync } from 'node:fs';
import { parseArgs } from 'node:util';
import { apkManifest, checkApk, sha256Of } from '../lib/apk-manifest.mjs';

try {
  const names = ['apk', 'version', 'url', 'out', 'verify'];
  const { values } = parseArgs({
    options: Object.fromEntries(names.map((name) => [name, { type: 'string' }])),
  });
  if (!values.apk) {
    throw new Error('--apk is required');
  }
  const apk = readFileSync(values.apk);
  if (values.verify) {
    const manifest = apkManifest(JSON.parse(readFileSync(values.verify, 'utf8')));
    checkApk(manifest, apk);
    console.error(`latest-manifest: ${values.apk} is the APK of ${values.verify}`);
  } else if (values.version && values.url && values.out) {
    const manifest = apkManifest({
      versionName: values.version,
      url: values.url,
      sha256: sha256Of(apk),
    });
    writeFileSync(values.out, `${JSON.stringify(manifest, null, 2)}\n`);
    console.error(`latest-manifest: versionCode ${manifest.versionCode} in ${values.out}`);
  } else {
    throw new Error('--version, --url and --out, or --verify, are required');
  }
} catch (error) {
  console.error(`latest-manifest: ${error.message}`);
  process.exitCode = 1;
}
