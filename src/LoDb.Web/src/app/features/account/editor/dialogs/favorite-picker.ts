import { DIALOG_DATA, DialogRef } from '@angular/cdk/dialog';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { Image } from '../../../../ui/media/image';
import { DialogFrame } from '../../../../ui/overlays/dialog-frame';
import { filterOptions } from '../entries/filter-options';
import { initialsOf } from '../entries/initials-of';
import type { PickerEntry } from '../entries/picker-entry';
import { loadPickerList } from '../picker/load-picker-list';
import type { PickerBodyStatus } from '../picker/picker-body-status';
import type { PickerLoad } from '../picker/picker-load';
import type { FavoritePickerData } from './favorite-picker-data';
import { PickerBody } from './picker-body';

/**
 * Picks the favorite of a slot: its list searched by name, whatever the accents, a rune
 * path offered with its runes. It closes on the entry picked, on null to empty the slot,
 * and on nothing when dismissed.
 */
@Component({
  selector: 'lodb-favorite-picker',
  imports: [DialogFrame, Image, PickerBody, TranslocoPipe],
  templateUrl: './favorite-picker.html',
  styleUrl: './picker.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class FavoritePicker {
  /** Id of the picker's heading, which names the dialog: the opener's `labelledBy`. */
  static readonly HEADING_ID = 'lodb-favorite-picker-heading';

  protected readonly data = inject<FavoritePickerData>(DIALOG_DATA);
  protected readonly ref = inject<DialogRef<PickerEntry | null>>(DialogRef);
  protected readonly headingId = FavoritePicker.HEADING_ID;
  protected readonly initialsOf = initialsOf;
  protected readonly query = signal('');
  private readonly list = signal<PickerLoad<PickerEntry>>({ status: 'loading' });

  protected readonly shown = computed(() => {
    const list = this.list();
    return list.status === 'ready' ? filterOptions(list.entries, this.query()) : [];
  });
  protected readonly status = computed<PickerBodyStatus>(() => {
    const status = this.list().status;
    return status === 'ready' && this.shown().length === 0 ? 'empty' : status;
  });

  constructor() {
    void this.fetch();
  }

  protected fetch(): Promise<void> {
    return loadPickerList(this.list, () => this.data.catalog.favorites(this.data.slot));
  }
}
