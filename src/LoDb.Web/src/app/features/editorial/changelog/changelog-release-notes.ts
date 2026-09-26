import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import type { ChangelogRelease } from './model/changelog-release';

/** What a release brings, as its file words it: intro, feature cards, fixes, developer note. */
@Component({
  selector: 'lodb-changelog-release-notes',
  imports: [TranslocoPipe],
  templateUrl: './changelog-release-notes.html',
  styleUrl: './changelog-release-notes.css',
  host: { class: 'block' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ChangelogReleaseNotes {
  readonly release = input.required<ChangelogRelease>();
  /** Language of the translated labels, when the notes around them are in another. */
  readonly labelLanguage = input<string | null>(null);
}
