import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import type { RobotHitView } from '../../../../../../core/api/generated/models/robot-hit-view';
import { Chip } from '../../../../../../ui/controls/chip';
import { FigurePipe } from '../../../../format/figure-pipe';
import { StampPipe } from '../../../../format/stamp-pipe';
import { AdminTextPipe } from '../../../../shared/admin-text-pipe';
import { injectAdminText } from '../../../../shared/inject-admin-text';
import { Badge } from '../../../../widgets/badge';
import { robotLabel } from './robot-label';
import { statusTone, verdictTone } from './robot-palette';

/** A hit as a row shows it: its robot named, its tones chosen. */
interface HitRow {
  readonly hit: RobotHitView;
  readonly robot: string;
  readonly statusTone: ReturnType<typeof statusTone>;
  readonly verdictTone: ReturnType<typeof verdictTone>;
}

/**
 * Hits of robots, newest first: when, which robot (a link to its page), how it was verified,
 * what it asked and how nginx answered it, in how long, and from which network. The robot's
 * column is left out of a robot's own page.
 */
@Component({
  selector: 'lodb-robot-hits-table',
  imports: [Badge, Chip, FigurePipe, RouterLink, StampPipe, AdminTextPipe],
  template: `
    <div class="hx-table-scroll">
      <table class="hx-table hx-table--flush admin-tbl">
        <thead>
          <tr>
            <th scope="col">{{ 'admin.robots.hits.at' | adminText }}</th>
            @if (showRobot()) {
              <th scope="col">{{ 'admin.robots.hits.robot' | adminText }}</th>
            }
            <th scope="col">{{ 'admin.robots.hits.verdict' | adminText }}</th>
            <th scope="col">{{ 'admin.robots.hits.status' | adminText }}</th>
            <th scope="col">{{ 'admin.robots.hits.target' | adminText }}</th>
            <th scope="col" class="num">{{ 'admin.robots.hits.duration' | adminText }}</th>
            <th scope="col" class="num">{{ 'admin.robots.hits.bytes' | adminText }}</th>
            <th scope="col">{{ 'admin.robots.hits.network' | adminText }}</th>
          </tr>
        </thead>
        <tbody>
          @for (row of rows(); track $index) {
            <tr>
              <td class="num whitespace-nowrap">{{ row.hit.at | stamp: 'second' }}</td>
              @if (showRobot()) {
                <td>
                  <a
                    class="text-gold-bright hover:text-hex"
                    [routerLink]="['/admin/robots', row.hit.robotKey]"
                    >{{ row.robot }}</a
                  >
                  <span class="cell-sub">{{
                    'admin.robots.categories.' + row.hit.category | adminText
                  }}</span>
                </td>
              }
              <td>
                <span [lodbBadge]="row.verdictTone">{{
                  'admin.robots.verdicts.' + row.hit.verdict | adminText
                }}</span>
              </td>
              <td>
                <span [lodbBadge]="row.statusTone">{{ row.hit.status }}</span>
              </td>
              <td>
                <span
                  class="block max-w-[28rem] truncate font-mono text-[0.78rem]"
                  [title]="row.hit.target"
                >
                  <span lodbChip>{{ row.hit.method }}</span> {{ row.hit.target }}
                </span>
                <span class="cell-sub">
                  {{ 'admin.robots.sections.' + row.hit.section | adminText }}
                  @if (row.hit.cache) {
                    · {{ row.hit.cache }}
                  }
                </span>
              </td>
              <td class="num">{{ row.hit.durationMs | figure }} ms</td>
              <td class="num">{{ row.hit.bytes | figure: 'bytes' }}</td>
              <td class="font-mono text-[0.78rem] text-text-muted">{{ row.hit.network || '—' }}</td>
            </tr>
          } @empty {
            <tr>
              <td [attr.colspan]="showRobot() ? 8 : 7" class="t-empty">
                {{ 'admin.robots.hits.empty' | adminText }}
              </td>
            </tr>
          }
        </tbody>
      </table>
    </div>
  `,
  host: { class: 'block' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RobotHitsTable {
  readonly hits = input.required<readonly RobotHitView[]>();
  readonly showRobot = input(true);

  private readonly texts = injectAdminText();

  protected readonly rows = computed<readonly HitRow[]>(() =>
    this.hits().map((hit) => ({
      hit,
      robot: robotLabel(
        { key: hit.robotKey, name: hit.robotName, operator: hit.operator, category: hit.category },
        this.texts,
      ),
      statusTone: statusTone(hit.status),
      verdictTone: verdictTone(hit.verdict),
    })),
  );
}
