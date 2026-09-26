import { ChangeDetectionStrategy, Component, inject, input, output } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { FAVORITE_SLOT } from '../../../../core/api/generated/models/favorite-slot-array';
import type { FavoriteSlot } from '../../../../core/api/generated/models/favorite-slot';
import { Field } from '../../../../ui/controls/field';
import { DialogService } from '../../../../ui/overlays/dialog-service';
import { FavoritePicker } from '../dialogs/favorite-picker';
import type { FavoritePickerData } from '../dialogs/favorite-picker-data';
import { SkinPicker } from '../dialogs/skin-picker';
import type { SkinPickerData } from '../dialogs/skin-picker-data';
import type { PickerEntry } from '../entries/picker-entry';
import { PickerCatalog } from '../picker/picker-catalog';
import { ProfileForm } from '../store/profile-form';
import type { SkinChoice } from '../store/skin-choice';
import { AutosaveIndicator } from './autosave-indicator';
import { FavoriteSocket } from './favorite-socket';
import { SkinSocket } from './skin-socket';

/**
 * The favorites of the editor: the version they are pinned to, the skin banner and the four
 * slots, each picked in a dialog and saved on its own after a pause.
 */
@Component({
  selector: 'lodb-favorites-panel',
  imports: [AutosaveIndicator, Field, FavoriteSocket, SkinSocket, TranslocoPipe],
  templateUrl: './favorites-panel.html',
  styleUrl: './favorites-panel.css',
  host: { class: 'block' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class FavoritesPanel {
  private readonly dialogs = inject(DialogService);
  private readonly catalog = inject(PickerCatalog);
  protected readonly form = inject(ProfileForm);
  protected readonly slots = FAVORITE_SLOT;

  /** The versions the favorites may be pinned to, newest first. */
  readonly versions = input.required<readonly string[]>();
  /** The version pinned; null to follow the one browsed. */
  readonly preferredVersion = input.required<string | null>();
  readonly busy = input(false);
  /** A new pin, or null to follow the version browsed again. */
  readonly pin = output<string | null>();

  protected pinFrom(select: HTMLSelectElement): void {
    this.pin.emit(select.value === '' ? null : select.value);
  }

  protected choose(slot: FavoriteSlot): void {
    const data: FavoritePickerData = {
      slot,
      catalog: this.catalog,
      current: this.form.favorites()[slot].id,
    };
    const labelledBy = FavoritePicker.HEADING_ID;
    this.dialogs
      .open<FavoritePicker, FavoritePickerData, PickerEntry | null>(FavoritePicker, {
        labelledBy,
        data,
      })
      .closed.subscribe((entry) => {
        if (entry !== undefined) {
          this.form.pick(slot, entry);
        }
      });
  }

  protected chooseSkin(): void {
    const data: SkinPickerData = { catalog: this.catalog, current: this.form.skin()?.id ?? null };
    this.dialogs
      .open<SkinPicker, SkinPickerData, SkinChoice | null>(SkinPicker, {
        labelledBy: SkinPicker.HEADING_ID,
        data,
      })
      .closed.subscribe((skin) => {
        if (skin !== undefined) {
          this.form.pickSkin(skin);
        }
      });
  }
}
