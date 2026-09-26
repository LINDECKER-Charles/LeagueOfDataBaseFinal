import { Directionality } from '@angular/cdk/bidi';
import { DIALOG_DATA } from '@angular/cdk/dialog';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { TranslocoPipe, provideTranslocoScope } from '@jsverse/transloco';
import { tabIndexAfter } from '../../../../../ui/tabs/tab-index-after';
import { DialogFrame } from '../../../../../ui/overlays/dialog-frame';
import type { SkinViewerData } from './skin-viewer-data';
import { ViewerArrows } from './viewer/viewer-arrows';
import { viewerIndexAfter } from './viewer/viewer-index-after';

/**
 * A skin's splash art whole, in a dialog: the arrows, and the arrow keys in the reading
 * direction, step through the gallery and wrap around; Escape closes, and the focus returns
 * to the tile that opened it.
 */
@Component({
  selector: 'lodb-skin-viewer',
  imports: [DialogFrame, TranslocoPipe, ViewerArrows],
  templateUrl: './skin-viewer.html',
  styleUrl: './skin-viewer.css',
  providers: [provideTranslocoScope('champions')],
  host: { '(keydown)': 'onKeydown($event)' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SkinViewer {
  static readonly HEADING_ID = 'skin-viewer-title';

  protected readonly headingId = SkinViewer.HEADING_ID;
  protected readonly data = inject<SkinViewerData>(DIALOG_DATA);
  protected readonly count = this.data.skins.length;
  protected readonly index = signal(this.data.index);
  protected readonly skin = computed(() => this.data.skins[this.index()]);
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
