import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { Skeleton } from '../../../../../ui/surfaces/skeleton';
import { injectChampionSkins } from './inject-champion-skins';
import { SkinGallery } from './skin-gallery';

/**
 * The skins section's content: placeholder tiles while the skins are read, then the
 * gallery. A read that failed twice leaves the section empty rather than loading forever.
 */
@Component({
  selector: 'lodb-skin-shelf',
  imports: [Skeleton, SkinGallery],
  template: `@if (loaded(); as skins) {
      <lodb-skin-gallery [skins]="skins" [championName]="championName()" />
    } @else if (skins.isLoading()) {
      <div class="flex gap-4 overflow-hidden" aria-hidden="true">
        @for (tile of placeholders; track tile) {
          <lodb-skeleton class="w-72 shrink-0" shape="tile" />
        }
      </div>
    }`,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SkinShelf {
  readonly version = input.required<string>();
  readonly language = input.required<string>();
  /** The champion's public id, such as MonkeyKing. */
  readonly champion = input.required<string>();
  readonly championName = input.required<string>();

  protected readonly placeholders = [1, 2, 3];
  protected readonly skins = injectChampionSkins(() => ({
    version: this.version(),
    lang: this.language(),
    id: this.champion(),
  }));
  protected readonly loaded = computed(() => (this.skins.hasValue() ? this.skins.value() : null));
}
