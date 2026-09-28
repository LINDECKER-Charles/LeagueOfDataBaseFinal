import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { RETURN_URL_PARAM } from '../../../core/auth/guards/return-url-param';
import { Button } from '../../../ui/controls/button';
import { Frame } from '../../../ui/surfaces/frame';
import { Skeleton } from '../../../ui/surfaces/skeleton';
import { AdminBand } from '../layout/admin-band';
import { ADMIN_PATHS } from '../shared/admin-paths';
import { AdminTextPipe } from '../shared/admin-text-pipe';
import type { Panel } from './panel';

/**
 * The shape of what a panel will show, which its placeholder reserves, as the legacy
 * skeletons had them: key figures alone, figures then a chart, figures then a row of cards,
 * figures then a table, or a single block (a page of its own, as the enrolment).
 */
export type PanelShape = 'kpi' | 'chart' | 'cards' | 'table' | 'block';

// Four figures head every panel of the legacy admin, then a chart, cards or a table.
const KPI_PLACEHOLDERS = [0, 1, 2, 3];
const CARD_PLACEHOLDERS = [0, 1, 2];
// Each card of the legacy placeholder: four lines, every other one shorter, over a bar.
const CARD_LINES = [0, 1, 2, 3];
const ROW_PLACEHOLDERS = [0, 1, 2, 3, 4, 5];

/**
 * What a panel shows while it has nothing else: placeholders shaped like its content until
 * its first answer, then the legacy red band that says why it failed, with a way to try
 * again. Once the panel has a value it renders nothing: the page shows the value, and a later
 * failure (of a refresh) still says so above it.
 */
@Component({
  selector: 'lodb-panel-state',
  imports: [AdminBand, Button, Frame, RouterLink, Skeleton, AdminTextPipe],
  templateUrl: './panel-state.html',
  host: { class: 'block' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PanelState {
  readonly panel = input.required<Panel<unknown>>();
  readonly shape = input<PanelShape>('table');

  private readonly router = inject(Router);

  protected readonly kpis = KPI_PLACEHOLDERS;
  protected readonly cards = CARD_PLACEHOLDERS;
  protected readonly lines = CARD_LINES;
  protected readonly rows = ROW_PLACEHOLDERS;
  protected readonly login = ADMIN_PATHS.login;
  // Read when a session failure shows: the login page brings the admin back here.
  protected readonly returnQuery = computed(() =>
    this.panel().failure()?.kind === 'session' ? { [RETURN_URL_PARAM]: this.router.url } : {},
  );
}
