import {
  ChangeDetectionStrategy,
  Component,
  type ElementRef,
  afterRenderEffect,
  computed,
  inject,
  input,
  viewChild,
} from '@angular/core';
import type { ChampionSkin } from '../../../../../core/api/generated/models/champion-skin';
import { DialogService } from '../../../../../ui/overlays/dialog-service';
import { alternateSkins } from './alternate-skins';
import { ChromaStrip } from './chromas/chroma-strip';
import { SkinViewer } from './skin-viewer';
import type { SkinViewerData } from './skin-viewer-data';

/**
 * The champion's skins as a strip of tiles, each opening its splash in the viewer, with
 * its chromas below it. The base skin is left out: the hero already wears it. The art is
 * hotlinked as the API gives it. The pager reuses the page for the next champion: the strip
 * then starts over at its first tile, as the legacy island, mounted afresh, did.
 */
@Component({
  selector: 'lodb-skin-gallery',
  imports: [ChromaStrip],
  templateUrl: './skin-gallery.html',
  styleUrl: './skin-gallery.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SkinGallery {
  readonly skins = input.required<readonly ChampionSkin[]>();
  readonly championName = input.required<string>();

  protected readonly tiles = computed(() => alternateSkins(this.skins()));
  private readonly dialogs = inject(DialogService);
  private readonly strip = viewChild.required<ElementRef<HTMLElement>>('strip');

  constructor() {
    afterRenderEffect(() => {
      this.tiles();
      // 0 is the inline start in both directions.
      this.strip().nativeElement.scrollLeft = 0;
    });
  }

  protected open(index: number): void {
    const data: SkinViewerData = {
      championName: this.championName(),
      skins: this.tiles(),
      index,
    };
    this.dialogs.open(SkinViewer, { labelledBy: SkinViewer.HEADING_ID, data, size: 'lightbox' });
  }
}
