// The constraints ADR 0007 puts on the Android project, read from the committed files.
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { describe, it } from 'node:test';

const web = new URL('../../../../src/LoDb.Web/', import.meta.url);
const read = (path) => readFileSync(new URL(path, web), 'utf8');
const { default: capacitor } = await import(new URL('capacitor.config.ts', web).href);

// Lower bound of a caret or tilde range, or of an exact version.
function floorOf(range) {
  const [major, minor, patch] = range.replace(/^[\^~]/, '').split('.').map(Number);
  return [major, minor, patch];
}

function atLeast(version, minimum) {
  for (let i = 0; i < minimum.length; i++) {
    if (version[i] !== minimum[i]) return version[i] > minimum[i];
  }
  return true;
}

describe('Capacitor configuration', () => {
  it('embeds the store build of the shell', () => {
    assert.equal(capacitor.webDir, 'dist/shell-store/browser');
    const angular = JSON.parse(read('angular.json'));
    const store = angular.projects['lodb-web'].architect.build.configurations['shell-store'];
    assert.equal(`${store.outputPath}/browser`, capacitor.webDir);
    assert.deepEqual(store.fileReplacements, [
      {
        replace: 'src/environments/environment.ts',
        with: 'src/environments/environment.store.ts',
      },
    ]);
  });

  it('serves the bundle from https://localhost, never from a remote server', () => {
    assert.equal(capacitor.server.androidScheme, 'https');
    assert.equal(capacitor.server.hostname, 'localhost');
    assert.equal(capacitor.server.url, undefined);
    assert.deepEqual(capacitor.server.allowNavigation, []);
    assert.equal(capacitor.android.allowMixedContent, false);
  });

  it('requires a Capacitor free of GHSA-rvm3-566m-v7fv (8.5.1 or later)', () => {
    const manifest = JSON.parse(read('package.json'));
    const ranges = { ...manifest.dependencies, ...manifest.devDependencies };
    for (const name of ['@capacitor/core', '@capacitor/android', '@capacitor/cli']) {
      assert.ok(ranges[name], `${name} is declared`);
      assert.ok(atLeast(floorOf(ranges[name]), [8, 5, 1]), `${name} ${ranges[name]}`);
    }
  });

  it('ships the plugins of ADR 0007', () => {
    const { dependencies } = JSON.parse(read('package.json'));
    for (const name of [
      '@capacitor/app',
      '@capacitor/browser',
      '@capacitor/share',
      '@aparajita/capacitor-secure-storage',
    ]) {
      assert.ok(dependencies[name], `${name} is a dependency`);
    }
  });
});

describe('Android project', () => {
  it('targets SDK 36 from SDK 24', () => {
    const variables = read('android/variables.gradle');
    assert.match(variables, /minSdkVersion = 24\b/);
    assert.match(variables, /compileSdkVersion = 36\b/);
    assert.match(variables, /targetSdkVersion = 36\b/);
  });

  it('verifies the App Link of the OAuth return and allows no clear text', () => {
    const manifest = read('android/app/src/main/AndroidManifest.xml');
    assert.match(manifest, /android:autoVerify="true"/);
    assert.match(manifest, /android:host="\$\{appLinkHost\}"/);
    assert.match(manifest, /android:pathPrefix="\/app\/oauth\/"/);
    assert.match(manifest, /android:usesCleartextTraffic="false"/);
  });

  it('keeps signing keys out of the repository', () => {
    const ignored = read('android/.gitignore').split('\n');
    assert.ok(ignored.includes('*.jks'));
    assert.ok(ignored.includes('*.keystore'));
  });
});
