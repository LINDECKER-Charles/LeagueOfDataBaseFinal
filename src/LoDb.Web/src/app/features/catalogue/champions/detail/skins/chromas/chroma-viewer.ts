import { Directionality } from '@angular/cdk/bidi';
import { DIALOG_DATA } from '@angular/cdk/dialog';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { TranslocoPipe, provideTranslocoScope } from '@jsverse/transloco';
import { DialogFrame } from '../../../../../../ui/overlays/dialog-frame';
import { tabIndexAfter } from '../../../../../../ui/tabs/tab-index-after';
import { ViewerArrows } from '../viewer/viewer-arrows';
import { viewerIndexAfter } from '../viewer/viewer-index-after';
import type { ChromaViewerData } from './chroma-viewer-data';

/**
 * A chroma up close, in a dialog: its label, its art and its accent colours. The arrows, and
 * the arrow keys in the reading direction, step through the skin's chromas and wrap around.
 */
@Component({
  selector: 'lodb-chroma-viewer',
  imports: [DialogFrame, TranslocoPipe, ViewerArrows],
  templateUrl: './chroma-viewer.html',
  styleUrl: './chroma-viewer.css',
  providers: [provideTranslocoScope('champions')],
  host: { '(keydown)': 'onKeydown($event)' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ChromaViewer {
  static readonly HEADING_ID = 'chroma-viewer-title';

  protected readonly headingId = ChromaViewer.HEADING_ID;
  protected readonly data = inject<ChromaViewerData>(DIALOG_DATA);
  protected readonly count = this.data.chromas.length;
  protected readonly index = signal(this.data.index);
  protected readonly chroma = computed(() => this.data.chromas[this.index()]);
  private readonly direction = inject(Directionality);

  protected step(move: 'previous' | 'next'): void {
    this.index.update((index) => tabIndexAfter(move, index, this.count));
  }

  protected onKeydown(event: KeyboardEvent): void {
    const at = { index: this.index(), count: this.count };
    const index = viewerIndexAfter(event.key, this.direction.value, at);
    if (index !== null) {
      event.preventDefault();
      this.index.set(index);
    }
  }
}
