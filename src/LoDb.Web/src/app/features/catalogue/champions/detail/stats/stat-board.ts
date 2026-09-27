import { ChangeDetectionStrategy, Component, computed, input, linkedSignal } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import type { ChampionCard } from '../../../../../core/api/generated/models/champion-card';
import { CatalogueImage } from '../../../../../ui/cards/catalogue-image';
import { formatStat } from './format-stat';
import { statAt } from './stat-at';
import { statRowsOf } from './stat-rows-of';

const MIN_LEVEL = 1;
const MAX_LEVEL = 18;
/** Box of the version-accurate icon, in CSS pixels. */
const ICON_SIZE = 56;

/**
 * The champion's base statistics and a level slider that scales them from 1 to 18. The
 * server renders level 1, readable as is; the slider starts over on each champion.
 */
@Component({
  selector: 'lodb-stat-board',
  imports: [CatalogueImage, TranslocoPipe],
  templateUrl: './stat-board.html',
  styleUrl: './stat-board.css',
  host: { class: 'hextech-frame hx-corners block p-6' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StatBoard {
  readonly profile = input.required<ChampionCard>();
  /** The version shown: the square icon is the one of that patch. */
  readonly version = input.required<string>();

  protected readonly minLevel = MIN_LEVEL;
  protected readonly maxLevel = MAX_LEVEL;
  protected readonly iconSize = ICON_SIZE;
  protected readonly level = linkedSignal(() => {
    this.profile();
    return MIN_LEVEL;
  });
  protected readonly rows = computed(() => {
    const level = this.level();
    return statRowsOf(this.profile()).map((row) => ({
      ...row,
      value: formatStat(statAt(row, level)),
    }));
  });

  protected onLevel(event: Event): void {
    this.level.set(Number((event.target as HTMLInputElement).value));
  }
}
