import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { type Observable, catchError, forkJoin, map, of, shareReplay, switchMap } from 'rxjs';
import type { ChangelogRelease } from './model/changelog-release';
import type { ManifestEntry } from './model/manifest-entry';
import { parseManifest } from './parsing/parse-manifest';
import { parseRelease } from './parsing/parse-release';

// Copied into the build from app/public/changelog/ (angular.json), where the release tooling
// of the current stack keeps writing them until the switch.
const CHANGELOG_DIR = 'changelog';

/**
 * The published changelog, read as static files of the build through HttpClient with a
 * relative URL, like the i18n catalogues: the prerender reads them from the build, the
 * transfer cache hands them to the browser, and the shell build finds them bundled. Never
 * the API. It never fails: a missing or corrupt file degrades to a shorter history.
 */
@Injectable({ providedIn: 'root' })
export class ChangelogReader {
  private readonly http = inject(HttpClient);
  private readonly manifest$ = this.http
    .get<unknown>(`${CHANGELOG_DIR}/manifest.json`)
    .pipe(map(parseManifest), shareReplay({ bufferSize: 1, refCount: false }));

  /** The manifest's entries, newest first. A failed read is not kept: the next one retries. */
  manifest(): Observable<readonly ManifestEntry[]> {
    return this.manifest$.pipe(catchError(() => of([])));
  }

  /**
   * The application's release: the newest manifest entry's version, the one number the
   * header, the footer and the changelog show. `null` without a manifest.
   */
  latestVersion(): Observable<string | null> {
    return this.manifest().pipe(map((entries) => entries[0]?.version ?? null));
  }

  /** The releases in the manifest's order; one whose file is missing is skipped. */
  releases(): Observable<readonly ChangelogRelease[]> {
    return this.manifest().pipe(
      switchMap((entries) =>
        entries.length === 0 ? of([]) : forkJoin(entries.map((entry) => this.release(entry.id))),
      ),
      map((releases) => releases.filter((release) => release !== null)),
    );
  }

  private release(id: string): Observable<ChangelogRelease | null> {
    return this.http.get<unknown>(`${CHANGELOG_DIR}/${id}.json`).pipe(
      map(parseRelease),
      catchError(() => of(null)),
    );
  }
}
