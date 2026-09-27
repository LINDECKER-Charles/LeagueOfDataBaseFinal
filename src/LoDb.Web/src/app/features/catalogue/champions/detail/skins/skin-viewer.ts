import { Directionality } from '@angular/cdk/bidi';
import { DIALOG_DATA, DialogRef } from '@angular/cdk/dialog';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { TranslocoPipe, provideTranslocoScope } from '@jsverse/transloco';
import { tabIndexAfter } from '../../../../../ui/tabs/tab-index-after';
import type { SkinViewerData } from './skin-viewer-data';
import { ViewerArrows } from './viewer/viewer-arrows';
import { viewerIndexAfter } from './viewer/viewer-index-after';

/**
 * A skin's splash art whole, in a lightbox: the picture itself, a close over its corner and
 * the skin named below it, as the legacy viewer. The arrows, and the arrow keys in the
 * reading direction, step through the gallery and wrap around; Escape closes, and the focus
 * returns to the tile that opened it.
 */
@Component({
  selector: 'lodb-skin-viewer',
  imports: [TranslocoPipe, ViewerArrows],
  templateUrl: './skin-viewer.html',
  styleUrl: './skin-viewer.css',
  providers: [provideTranslocoScope('champions')],
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
  private readonly ref = inject(DialogRef);

  constructor() {
    // The dialog's container, not this host, holds the focus once open: its keys reach the
    // overlay's stream, whichever element inside has the focus.
    this.ref.keydownEvents.pipe(takeUntilDestroyed()).subscribe((event) => {
      this.onKeydown(event);
    });
  }

  protected close(): void {
    this.ref.close();
  }

  protected step(move: 'previous' | 'next'): void {
    this.index.update((index) => tabIndexAfter(move, index, this.count));
  }

  private onKeydown(event: KeyboardEvent): void {
    const at = { index: this.index(), count: this.count };
    const index = viewerIndexAfter(event.key, this.direction.value, at);
    if (index !== null) {
      event.preventDefault();
      this.index.set(index);
    }
  }
}
