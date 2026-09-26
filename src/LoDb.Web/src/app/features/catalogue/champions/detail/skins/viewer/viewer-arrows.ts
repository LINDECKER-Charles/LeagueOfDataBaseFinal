import { ChangeDetectionStrategy, Component, output } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';

/**
 * The previous and next arrows laid over a viewer's picture, at its inline edges: the
 * previous one sits where reading starts, so they swap sides in a right-to-left page.
 */
@Component({
  selector: 'lodb-viewer-arrows',
  imports: [TranslocoPipe],
  template: `<button
      type="button"
      class="arrow arrow--previous"
      [attr.aria-label]="'champions.viewer.previous' | transloco"
      (click)="step.emit('previous')"
    >
      <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M15 6l-6 6 6 6" /></svg>
    </button>
    <button
      type="button"
      class="arrow arrow--next"
      [attr.aria-label]="'champions.viewer.next' | transloco"
      (click)="step.emit('next')"
    >
      <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M9 6l6 6-6 6" /></svg>
    </button>`,
  styleUrl: './viewer-arrows.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ViewerArrows {
  readonly step = output<'previous' | 'next'>();
}
