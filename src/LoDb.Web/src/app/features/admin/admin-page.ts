import { ChangeDetectionStrategy, Component, ViewEncapsulation, signal } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { Skeleton } from '../../ui/surfaces/skeleton';
import { NoticesOutlet } from './layout/notices-outlet';
import { OverviewPanel } from './panels/analytics/overview/overview-panel';

/**
 * The panels of the admin, `/admin/...`, under its frame: the band of the last action, then
 * the panel of the URL, each in its own chunk, loaded when opened. `/admin` itself shows the
 * overview, deferred too. Its guard lets in an administrator whose session was opened with a
 * second factor only (admin.routes.ts). It also carries the look the legacy panels shared
 * (tables, toolbars, slim fields), scoped under its host.
 */
@Component({
  selector: 'lodb-admin-page',
  imports: [NoticesOutlet, OverviewPanel, RouterOutlet, Skeleton],
  templateUrl: './admin-page.html',
  styleUrl: './admin-page.css',
  // The panels are separate components: their tables share one sheet, scoped by hand.
  encapsulation: ViewEncapsulation.None,
  host: { class: 'block' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AdminPage {
  /** Whether a panel of the URL fills the outlet; the overview shows otherwise. */
  protected readonly panelOpen = signal(false);
}
