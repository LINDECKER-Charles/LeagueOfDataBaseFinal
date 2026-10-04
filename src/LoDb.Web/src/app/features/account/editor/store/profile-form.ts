import { Injectable, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import type { FavoriteSlot } from '../../../../core/api/generated/models/favorite-slot';
import type { OwnerProfile } from '../../../../core/api/generated/models/owner-profile';
import { ProfileService } from '../../../../core/api/generated/services/profile.service';
import { ProfileAutosave } from '../autosave/profile-autosave';
import type { PickerEntry } from '../entries/picker-entry';
import type { FavoriteChoice } from './favorite-choice';
import type { FavoriteChoices } from './favorite-choices';
import { favoriteChoicesOf } from './favorite-choices-of';
import { favoritesBody } from './favorites-body';
import type { SkinChoice } from './skin-choice';
import { withoutRejected } from './without-rejected';

// The parts of the profile the autosave sends apart: a burst of picks is one request.
const FAVORITES = 'favorites';
const VISIBILITY = 'visibility';
const EMPTY: FavoriteChoice = { id: null, name: null, image: null };
const NO_FAVORITES: FavoriteChoices = {
  champion: EMPTY,
  item: EMPTY,
  rune: EMPTY,
  summoner: EMPTY,
};

/**
 * What the profile editor changes without a submit: the four favorites, the skin banner and
 * the visibility. Each change shows at once and is saved after a pause; a favorite the patch
 * lacks is saved back under its stored id, so it is never wiped by a save, and what the
 * server rejects is emptied, as it now stores it.
 */
@Injectable()
export class ProfileForm {
  private readonly profiles = inject(ProfileService);
  private readonly autosave = inject(ProfileAutosave);
  // The version the favorites resolved on, which the save checks them against.
  private version: string | undefined;

  readonly favorites = signal(NO_FAVORITES);
  readonly skin = signal<SkinChoice | null>(null);
  readonly isPublic = signal(false);

  /** Starts from the profile as the server resolved it. */
  load(profile: OwnerProfile): void {
    const { favorites, skin, version } = profile.showcase;
    this.favorites.set(favoriteChoicesOf(favorites));
    this.skin.set(skin === null ? null : { id: skin.id, name: skin.name, banner: skin.banner });
    this.isPublic.set(profile.isPublic);
    this.version = version ?? undefined;
  }

  /** Puts a picked entry in a slot, or empties it for null. */
  pick(slot: FavoriteSlot, entry: PickerEntry | null): void {
    const choice = entry === null ? EMPTY : { id: entry.id, name: entry.name, image: entry.image };
    this.favorites.update((choices) => ({ ...choices, [slot]: choice }));
    this.autosave.schedule(FAVORITES, () => this.saveFavorites());
  }

  /** Sets the skin banner, or removes it for null. */
  pickSkin(skin: SkinChoice | null): void {
    this.skin.set(skin);
    this.autosave.schedule(FAVORITES, () => this.saveFavorites());
  }

  /** Shows the public card to visitors, or hides it. */
  setPublic(isPublic: boolean): void {
    this.isPublic.set(isPublic);
    this.autosave.schedule(VISIBILITY, () => this.saveVisibility());
  }

  private async saveFavorites(): Promise<'saved' | 'warned'> {
    const sent = this.favorites();
    const skin = this.skin();
    const body = favoritesBody(sent, skin);
    const saved = await firstValueFrom(
      this.profiles.saveFavorites({ version: this.version, body }),
    );
    this.favorites.update((current) => withoutRejected(current, sent, saved.rejected));
    if (saved.isSkinRejected && this.skin() === skin) {
      this.skin.set(null);
    }
    return saved.rejected.length > 0 || saved.isSkinRejected ? 'warned' : 'saved';
  }

  private async saveVisibility(): Promise<'saved'> {
    const body = { isPublic: this.isPublic() };
    await firstValueFrom(this.profiles.setProfileVisibility({ body }));
    return 'saved';
  }
}
