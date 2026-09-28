import { ChangeDetectionStrategy, Component } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';

/**
 * The seal after the name of an author who supports the site: a small heart-gem in gold,
 * named for screen readers and on hover. The public profile has its twin; a feature cannot
 * share it. Plain paths, no ids: it repeats down the trends.
 */
@Component({
  selector: 'lodb-supporter-badge',
  imports: [TranslocoPipe],
  template: `
    @let label = 'community.supporter.badge' | transloco;
    <span class="supporter-badge" role="img" [attr.aria-label]="label" [title]="label">
      <svg viewBox="0 0 24 24" width="14" height="14" aria-hidden="true" focusable="false">
        <path
          fill="currentColor"
          d="M12 21 4.8 13.6a4.8 4.8 0 0 1 0-6.7 4.5 4.5 0 0 1 6.4 0l.8.8.8-.8a4.5 4.5 0 0 1 6.4
            0 4.8 4.8 0 0 1 0 6.7z"
        />
        <path fill="var(--color-hextech-black)" opacity="0.3" d="M12 8.6 8.2 12l3.8 4 3.8-4z" />
        <path fill="var(--color-gold-bright)" opacity="0.35" d="M12 8.6 8.2 12h7.6z" />
      </svg>
    </span>
  `,
  styles: `
    @layer components {
      .supporter-badge {
        display: inline-flex;
        align-items: center;
        color: var(--color-gold-rich);
        filter: drop-shadow(0 0 5px color-mix(in srgb, var(--color-gold) 55%, transparent));
      }
    }
  `,
  host: { class: 'inline-flex flex-none' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SupporterBadge {}
