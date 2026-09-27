import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { RETURN_URL_PARAM } from '../../../core/auth/guards/return-url-param';
import { Button } from '../../../ui/controls/button';
import { Frame } from '../../../ui/surfaces/frame';
import { Skeleton } from '../../../ui/surfaces/skeleton';
import { ADMIN_PATHS } from '../shared/admin-paths';
import type { Panel } from './panel';
import { AdminTextPipe } from '../shared/admin-text-pipe';

/**
 * The shape of what a panel will show, which its placeholder reserves: figures then a chart
 * or a table, or a single card.
 */
export type PanelShape = 'chart' | 'table' | 'card';

// Four figures head every panel of the legacy admin, then a chart or a table.
const KPI_PLACEHOLDERS = [0, 1, 2, 3];
const ROW_PLACEHOLDERS = [0, 1, 2, 3, 4, 5];

/**
 * What a panel shows while it has nothing else: placeholders shaped like its content until
 * its first answer, then the reason it failed with a way to try again. Once the panel has a
 * value it renders nothing: the page shows the value, and a later failure (of a refresh)
 * still says so above it.
 */
@Component({
  selector: 'lodb-panel-state',
  imports: [Button, Frame, RouterLink, Skeleton, AdminTextPipe],
  template: `
    @if (panel().failure(); as failure) {
      <div lodbFrame class="mb-6 block p-5" role="alert">
        <p class="font-beaufort text-lg text-gold">{{ 'admin.state.' + failure | adminText }}</p>
        <div class="mt-4 flex flex-wrap gap-2">
          <button lodbButton="ghost" type="button" (click)="panel().reload()">
            {{ 'admin.state.retry' | adminText }}
          </button>
          @if (failure === 'session') {
            <a lodbButton [routerLink]="login" [queryParams]="returnQuery()">
              {{ 'admin.state.sign_in' | adminText }}
            </a>
          }
        </div>
      </div>
    } @else if (panel().value() === undefined) {
      <div class="block" aria-busy="true">
        <p class="sr-only" role="status">{{ 'admin.state.loading' | adminText }}</p>
        @if (shape() === 'card') {
          <lodb-skeleton shape="block" />
        } @else {
          <div class="grid grid-cols-2 gap-3 lg:grid-cols-4">
            @for (kpi of kpis; track kpi) {
              <lodb-skeleton shape="tile" />
            }
          </div>
          <div class="mt-6">
            @if (shape() === 'chart') {
              <lodb-skeleton shape="block" />
            } @else {
              @for (row of rows; track row) {
                <lodb-skeleton shape="bar" class="mb-2" />
              }
            }
          </div>
        }
      </div>
    }
  `,
  host: { class: 'block' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PanelState {
  readonly panel = input.required<Panel<unknown>>();
  readonly shape = input<PanelShape>('table');

  private readonly router = inject(Router);

  protected readonly kpis = KPI_PLACEHOLDERS;
  protected readonly rows = ROW_PLACEHOLDERS;
  protected readonly login = ADMIN_PATHS.login;
  // Read when a session failure shows: the login page brings the admin back here.
  protected readonly returnQuery = computed(() =>
    this.panel().failure() === 'session' ? { [RETURN_URL_PARAM]: this.router.url } : {},
  );
}
