import { BreakpointObserver } from '@angular/cdk/layout';
import {
  ChangeDetectionStrategy,
  Component,
  DOCUMENT,
  Injector,
  afterNextRender,
  computed,
  effect,
  inject,
  linkedSignal,
  signal,
  untracked,
  viewChild,
} from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { firstValueFrom, map } from 'rxjs';
import type { PrerenderReport } from '../../../../../core/api/generated/models/prerender-report';
import { AdminNotices } from '../../../layout/admin-notices';
import { PageHead } from '../../../layout/page-head';
import { AdminTextPipe } from '../../../shared/admin-text-pipe';
import { injectAdminText } from '../../../shared/inject-admin-text';
import { PanelState } from '../../../state/panel-state';
import { Tab } from '../../../../../ui/tabs/tab';
import { Tabs } from '../../../../../ui/tabs/tabs';
import type { LaunchPreset } from './common/scope/launch-preset';
import { PRERENDER_CADENCE } from './common/scope/prerender-cadence';
import { CostsTab } from './costs/costs-tab';
import { SwitchDialog, type SwitchData } from './costs/switch/switch-dialog';
import { CoverageTab } from './coverage/coverage-tab';
import { injectAdminDialogs } from './dialogs/inject-admin-dialogs';
import { LaunchOpener } from './launch/dialog/launch-opener';
import { LiveMonitor } from './monitor/live-monitor';
import { PassRecap } from './monitor/recap/pass-recap';
import { entryPreset } from './overview/blocks/entry-preset';
import { IdleCard } from './overview/blocks/idle-card';
import { LaunchCta } from './overview/blocks/launch-cta';
import { PrerenderBands } from './overview/blocks/prerender-bands';
import { PrerenderKpis } from './overview/blocks/prerender-kpis';
import { bandStateOf } from './overview/state/band-state-of';
import { injectPrerenderReport } from './overview/state/inject-prerender-report';
import { launchLockOf } from './overview/state/launch-lock-of';
import { type PassTitle, passTitleOf } from './overview/state/pass-title-of';
import { quietPanel } from './overview/state/quiet-panel';
import { recentPassOf } from './overview/state/recent-pass-of';
import {
  PassDetailDialog,
  type PassDetailData,
  type PassDetailResult,
} from './passes/pass-detail-dialog';
import { PassesTab } from './passes/passes-tab';
import { SpeedBlock } from './speed/speed-block';

/** What the title of the document follows. */
interface TitleSource {
  readonly report: PrerenderReport | undefined;
  readonly dismissed: number | null;
}

// The pass a visitor dismissed from the recap, kept in this browser by PassRecap.
const DISMISSED_KEY = 'lodb.admin.prerender.recap-dismissed';
const MONITOR_ID = 'prerender-monitor';
// Under the `sm` breakpoint the three tabs must hold in 328px: « Coûts » stands for the costs.
const PHONE = '(max-width: 639.98px)';

/**
 * `/admin/prerender`: the pre-rendering of the older patches into the object store (§ 2).
 * Its head launches a pass and says how fresh the page is; bands say what needs doing; then
 * the live monitor of the active pass, the recap of one just ended, or what to do without
 * one; the key figures; and three tabs: coverage, storage and costs, history. The page alone
 * opens the launch dialog and the detail of a pass: every other block hands it a preset.
 */
@Component({
  selector: 'lodb-prerender-panel',
  imports: [
    AdminTextPipe,
    CostsTab,
    CoverageTab,
    IdleCard,
    LaunchCta,
    LiveMonitor,
    PageHead,
    PanelState,
    PassRecap,
    PassesTab,
    PrerenderBands,
    PrerenderKpis,
    SpeedBlock,
    Tab,
    Tabs,
  ],
  templateUrl: './prerender-panel.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PrerenderPanel {
  private readonly texts = injectAdminText();
  private readonly opener = inject(LaunchOpener);
  private readonly dialogs = injectAdminDialogs();
  private readonly cadence = inject(PRERENDER_CADENCE);
  private readonly document = inject(DOCUMENT);
  private readonly injector = inject(Injector);
  private readonly notices = inject(AdminNotices);
  private readonly breakpoints = inject(BreakpointObserver);
  private readonly coverage = viewChild(CoverageTab);
  // Once a pass starts, the focus goes to its monitor as soon as the report shows it.
  private readonly isFocusDue = signal(false);

  protected readonly isPhone = toSignal(
    this.breakpoints.observe(PHONE).pipe(map((state) => state.matches)),
    { initialValue: this.breakpoints.isMatched(PHONE) },
  );
  protected readonly report = injectPrerenderReport();
  protected readonly quiet = quietPanel(this.report);
  protected readonly dismissed = signal<number | null>(this.storedDismissal());
  protected readonly lock = computed(() => launchLockOf(this.report.value()));
  protected readonly bands = computed(() => {
    const report = this.report.value();
    return report ? bandStateOf(report) : null;
  });
  protected readonly recent = computed(() => {
    const report = this.report.value();
    return report ? recentPassOf(report, this.dismissed()) : null;
  });
  protected readonly reportEvery = computed(() =>
    this.report.value()?.current ? this.cadence.reportActiveMs : this.cadence.reportIdleMs,
  );
  private readonly title = linkedSignal<TitleSource, PassTitle>({
    source: () => ({ report: this.report.value(), dismissed: this.dismissed() }),
    computation: ({ report, dismissed }, previous) =>
      passTitleOf(report, dismissed, previous?.value.held ?? null),
  });
  protected readonly documentTitle = computed(() => {
    const { key, params } = this.title();
    return this.texts.text(key, params);
  });

  constructor() {
    effect(() => {
      if (this.isFocusDue() && this.report.value()?.current) {
        untracked(() => this.isFocusDue.set(false));
        afterNextRender(() => this.follow(), { injector: this.injector });
      }
    });
  }

  protected async openLaunch(preset: LaunchPreset): Promise<void> {
    const report = this.report.value();
    if (!report || this.lock() !== null) {
      return;
    }
    const started = await this.opener.open(preset, report);
    // Even closed without a pass: after a 409, « Fermer et suivre la passe » expects it.
    this.refresh();
    this.isFocusDue.set(started !== null);
  }

  protected launchFromHead(): void {
    void this.openLaunch(entryPreset('header', null));
  }

  protected completeEpoch(epoch: number): void {
    void this.openLaunch(entryPreset('complete-epoch', epoch));
  }

  /** Reads the report and the coverage again; the monitor reads itself. */
  protected refresh(): void {
    this.report.reload();
    this.coverage()?.reload();
  }

  /**
   * A pass seen ending: the band of its launch (« Passe n°2 lancée… ») would contradict the
   * recap that takes the monitor's place, so it goes; the page reads everything again.
   */
  protected onEnded(): void {
    this.notices.clear();
    this.refresh();
  }

  protected openSwitch(target: number): void {
    const report = this.report.value();
    if (report) {
      this.dialogs.open<SwitchDialog, SwitchData, void>(SwitchDialog, {
        labelledBy: SwitchDialog.HEADING_ID,
        data: { target, report },
      });
    }
  }

  protected async openDetail(id: number): Promise<void> {
    const report = this.report.value();
    const pass = report?.passes.find((one) => one.id === id) ?? report?.current;
    if (!report || pass?.id !== id) {
      return;
    }
    const ref = this.dialogs.open<PassDetailDialog, PassDetailData, PassDetailResult>(
      PassDetailDialog,
      { labelledBy: PassDetailDialog.HEADING_ID, data: { pass, report }, size: 'wide' },
    );
    const result = await firstValueFrom(ref.closed);
    if (result === 'follow') {
      afterNextRender(() => this.follow(), { injector: this.injector });
    } else if (result) {
      await this.openLaunch(result);
    }
  }

  /** Takes the operator to the monitor of the active pass. */
  protected follow(): void {
    this.document.getElementById(MONITOR_ID)?.focus();
  }

  protected dismiss(id: number): void {
    this.dismissed.set(id);
  }

  private storedDismissal(): number | null {
    try {
      const stored = this.document.defaultView?.localStorage.getItem(DISMISSED_KEY);
      return stored ? Number(stored) : null;
    } catch {
      // Private browsing or storage refused: nothing was dismissed in this browser.
      return null;
    }
  }
}
