import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { ChangelogReleaseNotes } from './changelog-release-notes';
import { formatReleaseDate } from './format-release-date';
import type { ChangelogRelease } from './model/changelog-release';

// The release tooling writes the files in French.
const RELEASE_LANGUAGE = 'fr';

/**
 * One release of the timeline. Its whole body sits in a native `<details>`: it stays in the
 * DOM, readable by crawlers and without JavaScript, while collapsing keeps the page
 * scannable. Only the chrome is translated; the release texts are shown as written.
 */
@Component({
  selector: 'lodb-changelog-release',
  imports: [ChangelogReleaseNotes, TranslocoPipe],
  templateUrl: './changelog-release-item.html',
  styleUrl: './changelog-release-item.css',
  host: { class: 'block' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ChangelogReleaseItem {
  readonly release = input.required<ChangelogRelease>();
  /** Expanded on arrival: the latest release. */
  readonly open = input(false);
  /** The page locale: the release texts are marked French under any other. */
  readonly locale = input.required<string>();

  protected readonly date = computed(() => formatReleaseDate(this.release().date));
  protected readonly contentLanguage = computed(() =>
    this.locale() === RELEASE_LANGUAGE ? null : RELEASE_LANGUAGE,
  );
}
