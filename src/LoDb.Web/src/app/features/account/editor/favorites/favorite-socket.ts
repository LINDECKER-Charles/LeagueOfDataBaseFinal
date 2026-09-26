import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import type { FavoriteSlot } from '../../../../core/api/generated/models/favorite-slot';
import { Image } from '../../../../ui/media/image';
import { initialsOf } from '../entries/initials-of';
import type { FavoriteChoice } from '../store/favorite-choice';

/**
 * A favorite slot of the editor, which opens its picker: the favorite framed, a hollow
 * mount when empty, or a warning frame for a favorite the patch lacks, kept all the same.
 */
@Component({
  selector: 'lodb-favorite-socket',
  imports: [Image, TranslocoPipe],
  template: `@let type = 'profile.slot.' + slot() | transloco;
    <button
      type="button"
      class="socket socket--action"
      [class]="frame()"
      [attr.aria-label]="type + ' — ' + (choice().name ?? (caption() | transloco))"
      (click)="choose.emit()"
    >
      <span class="socket__type">{{ type }}</span>
      @if (choice().name; as name) {
        <lodb-image
          class="socket__portrait"
          [src]="choice().image"
          [alt]="name"
          [width]="72"
          [height]="72"
          [initials]="initials()"
          imgClass="object-cover"
        />
        <span class="socket__name">{{ name }}</span>
      } @else {
        <span class="socket__hex" aria-hidden="true"></span>
        <span class="socket__name socket__name--muted">{{ caption() | transloco }}</span>
      }
    </button>`,
  styleUrl: './favorite-socket.css',
  host: { class: 'block' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class FavoriteSocket {
  readonly slot = input.required<FavoriteSlot>();
  readonly choice = input.required<FavoriteChoice>();
  readonly choose = output();

  protected readonly initials = computed(() => initialsOf(this.choice().name ?? ''));
  protected readonly frame = computed(() => {
    const { id, name } = this.choice();
    if (name !== null) {
      return 'hextech-frame hx-corners socket--filled';
    }
    return id === null ? 'socket--empty' : 'socket--unavailable';
  });
  protected readonly caption = computed(() =>
    this.choice().id === null ? 'profile.favorites.empty' : 'profile.favorites.unavailable',
  );
}
