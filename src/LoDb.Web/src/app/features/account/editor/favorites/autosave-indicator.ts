import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { ProfileAutosave } from '../autosave/profile-autosave';

const MESSAGES = {
  idle: null,
  saving: 'profile.autosave.saving',
  saved: 'profile.autosave.saved',
  warned: 'profile.autosave.dropped',
  error: 'profile.autosave.error',
} as const;

/**
 * Tells how the automatic save of the profile goes, politely to a screen reader; a failed
 * save offers to send it again.
 */
@Component({
  selector: 'lodb-autosave-indicator',
  imports: [TranslocoPipe],
  template: `<span class="autosave-status" [attr.data-status]="status()" aria-live="polite">
      @if (message(); as key) {
        {{ key | transloco }}
      }
    </span>
    @if (status() === 'error') {
      <button type="button" class="autosave-retry" (click)="retry()">
        {{ 'profile.picker.retry' | transloco }}
      </button>
    }`,
  styleUrl: './autosave-indicator.css',
  // The band only takes room when the retry shows: idle, nothing sits under the sockets.
  host: {
    class: 'flex items-center justify-end gap-4',
    '[class.min-h-11]': "status() === 'error'",
  },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AutosaveIndicator {
  private readonly autosave = inject(ProfileAutosave);

  protected readonly status = this.autosave.status;
  protected readonly message = computed(() => MESSAGES[this.status()]);

  protected retry(): void {
    void this.autosave.retry();
  }
}
