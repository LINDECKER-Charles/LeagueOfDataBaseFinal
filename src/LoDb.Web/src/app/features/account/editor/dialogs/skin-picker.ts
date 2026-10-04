import { DIALOG_DATA, DialogRef } from '@angular/cdk/dialog';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { Icon } from '../../../../ui/media/icon';
import { Image } from '../../../../ui/media/image';
import { DialogFrame } from '../../../../ui/overlays/dialog-frame';
import { filterOptions } from '../entries/filter-options';
import { initialsOf } from '../entries/initials-of';
import type { PickerEntry } from '../entries/picker-entry';
import type { SkinEntry } from '../entries/skin-entry';
import { loadPickerList } from '../picker/load-picker-list';
import type { PickerBodyStatus } from '../picker/picker-body-status';
import type { PickerLoad } from '../picker/picker-load';
import type { SkinChoice } from '../store/skin-choice';
import { PickerBody } from './picker-body';
import type { SkinPickerData } from './skin-picker-data';

/**
 * Picks the skin banner in two steps: a champion, then one of its skins shown as tiles of
 * their art. It closes on the skin picked, on null to remove the banner, and on nothing
 * when dismissed.
 */
@Component({
  selector: 'lodb-skin-picker',
  imports: [DialogFrame, Icon, Image, PickerBody, TranslocoPipe],
  templateUrl: './skin-picker.html',
  styleUrls: ['./picker.css', './skin-picker.css'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SkinPicker {
  /** Id of the picker's heading, which names the dialog: the opener's `labelledBy`. */
  static readonly HEADING_ID = 'lodb-skin-picker-heading';

  protected readonly data = inject<SkinPickerData>(DIALOG_DATA);
  protected readonly ref = inject<DialogRef<SkinChoice | null>>(DialogRef);
  protected readonly headingId = SkinPicker.HEADING_ID;
  protected readonly initialsOf = initialsOf;
  protected readonly query = signal('');
  /** The champion whose skins are shown; null on the first step. */
  protected readonly champion = signal<PickerEntry | null>(null);
  private readonly champions = signal<PickerLoad<PickerEntry>>({ status: 'loading' });
  private readonly skins = signal<PickerLoad<SkinEntry>>({ status: 'loading' });

  protected readonly shownChampions = computed(() => this.filtered(this.champions()));
  protected readonly shownSkins = computed(() => this.filtered(this.skins()));
  protected readonly status = computed<PickerBodyStatus>(() => {
    const onSkins = this.champion() !== null;
    const status = (onSkins ? this.skins() : this.champions()).status;
    const count = onSkins ? this.shownSkins().length : this.shownChampions().length;
    return status === 'ready' && count === 0 ? 'empty' : status;
  });

  constructor() {
    void this.fetch();
  }

  protected fetch(): Promise<void> {
    const champion = this.champion();
    return champion === null
      ? loadPickerList(this.champions, () => this.data.catalog.favorites('champion'))
      : loadPickerList(this.skins, () => this.data.catalog.skins(champion.id));
  }

  protected open(champion: PickerEntry, search: HTMLInputElement): void {
    this.champion.set(champion);
    this.clear(search);
    void this.fetch();
  }

  protected back(search: HTMLInputElement): void {
    this.champion.set(null);
    this.clear(search);
  }

  protected pick(skin: SkinEntry): void {
    this.ref.close({ id: skin.id, name: skin.name, banner: skin.banner });
  }

  private clear(search: HTMLInputElement): void {
    search.value = '';
    this.query.set('');
    search.focus();
  }

  private filtered<T extends PickerEntry>(list: PickerLoad<T>): T[] {
    return list.status === 'ready' ? filterOptions(list.entries, this.query()) : [];
  }
}
