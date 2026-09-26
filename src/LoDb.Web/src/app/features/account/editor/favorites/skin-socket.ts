import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import type { SkinChoice } from '../store/skin-choice';

/** The skin banner of the editor, a wide socket over its art, which opens the skin picker. */
@Component({
  selector: 'lodb-skin-socket',
  imports: [TranslocoPipe],
  template: `@let label = 'profile.slot.skin' | transloco;
    @let empty = 'profile.skin.empty' | transloco;
    <button
      type="button"
      class="skin-socket"
      [class]="
        skin() === null ? 'skin-socket--empty' : 'skin-socket--filled hextech-frame hx-corners'
      "
      [attr.aria-label]="label + ' — ' + (skin()?.name ?? empty)"
      (click)="choose.emit()"
    >
      @if (skin(); as current) {
        <img
          class="hx-img skin-socket__art"
          [src]="current.banner"
          alt=""
          width="1280"
          height="720"
          loading="lazy"
          decoding="async"
        />
        <span class="skin-socket__scrim" aria-hidden="true"></span>
      }
      <span class="skin-socket__body">
        <span class="skin-socket__type">{{ label }}</span>
        <span class="skin-socket__name" [class.skin-socket__name--muted]="skin() === null">
          {{ skin()?.name ?? empty }}
        </span>
      </span>
      <span class="skin-socket__cta" aria-hidden="true">
        {{ (skin() === null ? 'profile.slot.skin' : 'profile.skin.change') | transloco }}
      </span>
    </button>`,
  styleUrl: './skin-socket.css',
  host: { class: 'block' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SkinSocket {
  readonly skin = input.required<SkinChoice | null>();
  readonly choose = output();
}
