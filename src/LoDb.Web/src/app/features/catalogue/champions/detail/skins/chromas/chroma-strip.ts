import { ChangeDetectionStrategy, Component, inject, input } from '@angular/core';
import { TranslocoPipe, provideTranslocoScope } from '@jsverse/transloco';
import type { ChampionChroma } from '../../../../../../core/api/generated/models/champion-chroma';
import { DialogService } from '../../../../../../ui/overlays/dialog-service';
import { ChromaViewer } from './chroma-viewer';
import type { ChromaViewerData } from './chroma-viewer-data';
import { chromaRamp } from './chroma-ramp';

/**
 * A skin's chromas as a row of swatches over the ramp of their accent colours, each opening
 * the chroma viewer. The label is the API's: the colour family the swatch reads as.
 */
@Component({
  selector: 'lodb-chroma-strip',
  imports: [TranslocoPipe],
  templateUrl: './chroma-strip.html',
  styleUrl: './chroma-strip.css',
  providers: [provideTranslocoScope('champions')],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ChromaStrip {
  readonly skinName = input.required<string>();
  readonly chromas = input.required<readonly ChampionChroma[]>();

  protected readonly ramp = chromaRamp;
  private readonly dialogs = inject(DialogService);

  protected open(index: number): void {
    const data: ChromaViewerData = { skinName: this.skinName(), chromas: this.chromas(), index };
    this.dialogs.open(ChromaViewer, {
      labelledBy: ChromaViewer.HEADING_ID,
      data,
      variant: 'compact',
    });
  }
}
