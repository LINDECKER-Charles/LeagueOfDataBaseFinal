import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import type { ApiKeyOverview } from '../../../core/api/generated/models/api-key-overview';
import { Button } from '../../../ui/controls/button';
import { Chip } from '../../../ui/controls/chip';
import { formatCount, formatDate, quotaPercent } from './usage-format';

/**
 * The active key at a glance: its start, name and date, the month's quota meter, its
 * credits and rate; then its regeneration and revocation.
 */
@Component({
  selector: 'lodb-key-overview',
  imports: [Button, Chip, TranslocoPipe],
  templateUrl: './key-overview.html',
  styleUrls: ['../shared/portal.css', './key.css'],
  host: { class: 'block' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class KeyOverview {
  readonly key = input.required<ApiKeyOverview>();
  readonly busy = input(false);

  readonly regenerate = output();
  readonly revoke = output();

  protected readonly created = computed(() => formatDate(this.key().createdAt));
  protected readonly quota = computed(() => {
    const key = this.key();
    return {
      used: formatCount(key.usedThisMonth),
      quota: formatCount(key.monthlyQuota),
      remaining: formatCount(key.remainingThisMonth),
      percent: quotaPercent(key.usedThisMonth, key.monthlyQuota),
    };
  });
  protected readonly credits = computed(() => formatCount(this.key().creditsBalance));
}
