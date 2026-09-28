import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import type { PickerBodyStatus } from '../picker/picker-body-status';

/**
 * The scrolling body of a picker dialog: the rows once the list is there, else what keeps
 * them, with a way to ask again after a failure. A dialog lives out of the page's injector,
 * so its texts are the root catalogue's.
 */
@Component({
  selector: 'lodb-picker-body',
  imports: [TranslocoPipe],
  template: `@switch (status()) {
    @case ('loading') {
      <p class="picker-note" role="status">{{ 'profile.picker.loading' | transloco }}</p>
    }
    @case ('failed') {
      <div class="picker-note">
        <p role="alert">{{ 'profile.picker.error' | transloco }}</p>
        <button type="button" class="picker-retry" (click)="retry.emit()">
          {{ 'profile.picker.retry' | transloco }}
        </button>
      </div>
    }
    @case ('empty') {
      <p class="picker-note" role="status">{{ 'filter.empty' | transloco }}</p>
    }
    @default {
      <ng-content />
    }
  }`,
  styleUrl: './picker-body.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PickerBody {
  readonly status = input.required<PickerBodyStatus>();
  readonly retry = output();
}
