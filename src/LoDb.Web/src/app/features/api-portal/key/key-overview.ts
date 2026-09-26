import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import type { ApiKeyOverview } from '../../../core/api/generated/models/api-key-overview';
import { Button } from '../../../ui/controls/button';
import { Chip } from '../../../ui/controls/chip';
import { Frame } from '../../../ui/surfaces/frame';
import { formatCount, formatDate, quotaPercent } from './usage-format';

/**
 * The active key at a glance: its start, name and date, the month's quota meter, its
 * credits and rate; then its regeneration and revocation.
 */
@Component({
  selector: 'lodb-key-overview',
  imports: [Button, Chip, Frame, TranslocoPipe],
  templateUrl: './key-overview.html',
  styleUrls: ['../shared/portal.css', './key.css'],
  host: { class: 'block' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class KeyOverview {
  readonly key = input.required<ApiKeyOverview>();
  readonly locale = input.required<string>();
  readonly busy = input(false);

  readonly regenerate = output();
  readonly revoke = output();

  protected readonly created = computed(() => formatDate(this.locale(), this.key().createdAt));
  protected readonly quota = computed(() => {
    const key = this.key();
    const count = (value: number) => formatCount(this.locale(), value);
    return {
      used: count(key.usedThisMonth),
      quota: count(key.monthlyQuota),
      remaining: count(key.remainingThisMonth),
      percent: quotaPercent(key.usedThisMonth, key.monthlyQuota),
    };
  });
  protected readonly credits = computed(() =>
    formatCount(this.locale(), this.key().creditsBalance),
  );
}
