import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import type { ChampionSkin } from '../../../../../core/api/generated/models/champion-skin';
import { DialogService } from '../../../../../ui/overlays/dialog-service';
import { alternateSkins } from './alternate-skins';
import { ChromaStrip } from './chromas/chroma-strip';
import { SkinViewer } from './skin-viewer';
import type { SkinViewerData } from './skin-viewer-data';

/**
 * The champion's skins as a strip of tiles, each opening its splash in the viewer, with
 * its chromas below it. The base skin is left out: the hero already wears it. The art is
 * hotlinked as the API gives it.
 */
@Component({
  selector: 'lodb-skin-gallery',
  imports: [ChromaStrip, TranslocoPipe],
  templateUrl: './skin-gallery.html',
  styleUrl: './skin-gallery.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SkinGallery {
  readonly skins = input.required<readonly ChampionSkin[]>();
  readonly championName = input.required<string>();

  protected readonly tiles = computed(() => alternateSkins(this.skins()));
  private readonly dialogs = inject(DialogService);

  protected open(index: number): void {
    const data: SkinViewerData = {
      championName: this.championName(),
      skins: this.tiles(),
      index,
    };
    this.dialogs.open(SkinViewer, { labelledBy: SkinViewer.HEADING_ID, data });
  }
}
