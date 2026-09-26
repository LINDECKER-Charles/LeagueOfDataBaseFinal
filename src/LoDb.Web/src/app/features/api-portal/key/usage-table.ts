import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import type { ApiKeyOverview } from '../../../core/api/generated/models/api-key-overview';
import { Frame } from '../../../ui/surfaces/frame';
import { formatCount, formatDay } from './usage-format';

/**
 * The metered days of the last 30, newest first, as `/v1` wrote them (a second of lag); a
 * quiet day has no row.
 */
@Component({
  selector: 'lodb-usage-table',
  imports: [Frame, TranslocoPipe],
  template: `
    <section lodbFrame class="portal-panel" aria-labelledby="api-usage-title">
      <div class="portal-panel__head">
        <h2 id="api-usage-title" class="portal-panel__title">
          {{ 'api.portal.usage.title' | transloco }}
        </h2>
        <span class="codex-header__meta">
          {{ 'api.portal.usage.total_month' | transloco }} · {{ total() }}
        </span>
      </div>
      @if (days().length === 0) {
        <p class="portal-text">{{ 'api.portal.usage.empty' | transloco }}</p>
      } @else {
        <div class="hx-table-scroll mt-4">
          <table class="hx-table hx-table--flush" data-testid="api-usage">
            <thead>
              <tr>
                <th scope="col">{{ 'api.portal.usage.day' | transloco }}</th>
                <th scope="col" class="num">{{ 'api.portal.usage.requests' | transloco }}</th>
              </tr>
            </thead>
            <tbody>
              @for (day of days(); track day.day) {
                <tr>
                  <td>{{ day.label }}</td>
                  <td class="num">{{ day.requests }}</td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      }
    </section>
  `,
  styleUrl: '../shared/portal.css',
  host: { class: 'block' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class UsageTable {
  readonly key = input.required<ApiKeyOverview>();
  readonly locale = input.required<string>();

  protected readonly total = computed(() => formatCount(this.locale(), this.key().usedThisMonth));
  protected readonly days = computed(() =>
    this.key().usage.map((day) => ({
      day: day.day,
      label: formatDay(this.locale(), day.day),
      requests: formatCount(this.locale(), day.requests),
    })),
  );
}
