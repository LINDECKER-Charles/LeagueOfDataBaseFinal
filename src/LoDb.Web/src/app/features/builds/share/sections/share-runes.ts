import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import type { PerkView } from '../../../../core/api/generated/models/perk-view';
import type { RunePageView } from '../../../../core/api/generated/models/rune-page-view';
import type { RunePathView } from '../../../../core/api/generated/models/rune-path-view';
import { imageSource } from '../../shared/media/image-source';

/** A rune or a path as the page draws it: its name, its icon, and whether it is a ghost. */
interface RuneMark {
  readonly name: string;
  readonly icon: string | null;
  readonly missing: boolean;
}

function markOf(rune: PerkView | RunePathView): RuneMark {
  return { name: rune.name, icon: imageSource(rune.icon), missing: rune.missing };
}

/**
 * The rune page of a shared build: the primary path with its keystone and three minor runes,
 * tinted by the path, then the secondary path and its two runes. A rune its patch lacks
 * stays in its row, dimmed, its id for a name.
 */
@Component({
  selector: 'lodb-share-runes',
  imports: [TranslocoPipe],
  templateUrl: './share-runes.html',
  styleUrl: './share-runes.css',
  host: { class: 'block mt-12' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ShareRunes {
  readonly runes = input.required<RunePageView>();

  protected readonly primary = computed(() => markOf(this.runes().primary));
  protected readonly secondary = computed(() => markOf(this.runes().secondary));
  protected readonly keystone = computed(() => markOf(this.runes().keystone));
  protected readonly minors = computed(() => this.runes().minors.map(markOf));
  protected readonly secondaryPerks = computed(() => this.runes().secondaryPerks.map(markOf));
}
