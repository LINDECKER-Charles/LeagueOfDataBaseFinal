import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ApplicationInitStatus } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';
import { RELEASE_VERSION } from '../../../core/layout/shell/release-version';
import { ChangelogReader } from './changelog-reader';
import { provideReleaseVersion } from './provide-release-version';

const MANIFEST = {
  patches: [
    { id: '2026-08-22-tamis', version: '2.2.1' },
    { id: '2026-08-22-jade', version: '2.2.0' },
    { id: '2026-08-09-heraut', version: '2.1.0' },
  ],
};

function release(version: string, codename: string) {
  return { version, codename, date: '2026-08-22', type: 'minor', summary: codename };
}

describe('ChangelogReader', () => {
  let http: HttpTestingController;
  let reader: ChangelogReader;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpTestingController);
    reader = TestBed.inject(ChangelogReader);
  });

  afterEach(() => {
    http.verify();
  });

  it('reads the releases from the build files, in the manifest order', async () => {
    const releases = firstValueFrom(reader.releases());

    http.expectOne('changelog/manifest.json').flush(MANIFEST);
    http.expectOne('changelog/2026-08-22-tamis.json').flush(release('2.2.1', 'Tamis'));
    http.expectOne('changelog/2026-08-22-jade.json').flush(release('2.2.0', 'Jade'));
    http.expectOne('changelog/2026-08-09-heraut.json').flush(release('2.1.0', 'Héraut'));

    expect((await releases).map((entry) => entry.codename)).toEqual(['Tamis', 'Jade', 'Héraut']);
  });

  it('skips a release whose file is missing or unreadable', async () => {
    const releases = firstValueFrom(reader.releases());

    http.expectOne('changelog/manifest.json').flush(MANIFEST);
    http.expectOne('changelog/2026-08-22-tamis.json').flush(release('2.2.1', 'Tamis'));
    http
      .expectOne('changelog/2026-08-22-jade.json')
      .flush('Not Found', { status: 404, statusText: 'Not Found' });
    http.expectOne('changelog/2026-08-09-heraut.json').flush({ codename: 'No version' });

    expect((await releases).map((entry) => entry.version)).toEqual(['2.2.1']);
  });

  it('reads the manifest once for every reader', async () => {
    const version = firstValueFrom(reader.latestVersion());
    const entries = firstValueFrom(reader.manifest());

    http.expectOne('changelog/manifest.json').flush(MANIFEST);

    expect(await version).toBe('2.2.1');
    expect(await entries).toHaveLength(3);
  });

  it('reads a corrupt manifest as an empty history, without a file request', async () => {
    const releases = firstValueFrom(reader.releases());

    http.expectOne('changelog/manifest.json').flush({ releases: [] });

    expect(await releases).toEqual([]);
  });

  it('retries a manifest whose read failed', async () => {
    const failed = firstValueFrom(reader.latestVersion());
    http.expectOne('changelog/manifest.json').error(new ProgressEvent('offline'));
    expect(await failed).toBeNull();

    const retried = firstValueFrom(reader.latestVersion());
    http.expectOne('changelog/manifest.json').flush(MANIFEST);
    expect(await retried).toBe('2.2.1');
  });
});

describe('provideReleaseVersion', () => {
  async function versionAfterStartup(answer: (http: HttpTestingController) => void) {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideReleaseVersion()],
    });
    const status = TestBed.inject(ApplicationInitStatus);
    answer(TestBed.inject(HttpTestingController));
    await status.donePromise;
    return TestBed.inject(RELEASE_VERSION);
  }

  it('gives the header and the footer the newest release, before the first render', async () => {
    const version = await versionAfterStartup((http) =>
      http.expectOne('changelog/manifest.json').flush(MANIFEST),
    );

    expect(version).toBe('2.2.1');
  });

  it('leaves the version unknown without a manifest', async () => {
    const version = await versionAfterStartup((http) =>
      http
        .expectOne('changelog/manifest.json')
        .flush('Not Found', { status: 404, statusText: 'Not Found' }),
    );

    expect(version).toBeNull();
  });
});
