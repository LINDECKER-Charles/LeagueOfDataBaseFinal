import { DIALOG_DATA } from '@angular/cdk/dialog';
import { ChangeDetectionStrategy, Component, type Signal, computed, inject } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { Icon } from '../../../../ui/media/icon';
import type { IconName } from '../../../../ui/media/icon-name';
import type { ResourceType } from '../../../api/generated/models/resource-type';
import type { WarmUpProgress } from '../../../api/generated/models/warm-up-progress';
import { percentOf } from '../percent-of';
import { LoaderCore } from './loader-core';

interface ResourceRow {
  readonly icon: IconName;
  /** Root catalogue key: the header's name of the list. */
  readonly label: string;
}

const ROWS: Record<ResourceType, ResourceRow> = {
  champions: { icon: 'champion', label: 'header.navigation.champion' },
  items: { icon: 'item', label: 'header.navigation.item' },
  runes: { icon: 'rune', label: 'header.navigation.runes' },
  summoners: { icon: 'spell', label: 'header.navigation.summoner' },
};

/**
 * The loader's modal: the lists the destination shows, each turning ready once its images
 * have landed, the entry landing now, and the bar of the images fetched. It renders the
 * frames `WarmUpLoader` hands it, never a figure of its own: the bar is what the API
 * stored, and "ready" is what it said.
 */
@Component({
  selector: 'lodb-loader-dialog',
  imports: [Icon, LoaderCore, TranslocoPipe],
  templateUrl: './loader-dialog.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LoaderDialog {
  /** Id of the dialog's heading, which names it: the opener's `labelledBy`. */
  static readonly HEADING_ID = 'lodb-loader-heading';

  protected readonly headingId = LoaderDialog.HEADING_ID;
  protected readonly rows = ROWS;
  protected readonly frame = inject<Signal<WarmUpProgress>>(DIALOG_DATA);
  protected readonly preparing = computed(() => this.frame().stage === 'preparing');
  protected readonly percent = computed(() => percentOf(this.frame()));
  /** The entry landing now, as a list of one: a new name renders anew, and slides in. */
  protected readonly landing = computed(() => {
    const latest = this.frame().latest;
    return latest ? [latest] : [];
  });
}
