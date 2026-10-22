import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  Injector,
  afterNextRender,
  computed,
  inject,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { Button } from '../../../../../../../ui/controls/button';
import { DialogFrame } from '../../../../../../../ui/overlays/dialog-frame';
import { Skeleton } from '../../../../../../../ui/surfaces/skeleton';
import { duration } from '../../../../../format/duration';
import { AdminBand } from '../../../../../layout/admin-band';
import { ADMIN_PATHS } from '../../../../../shared/admin-paths';
import { AdminTextPipe } from '../../../../../shared/admin-text-pipe';
import { injectAdminText } from '../../../../../shared/inject-admin-text';
import type { LaunchField, LaunchMessage, LaunchStep } from '../call/launch-errors-of';
import { EpochCards } from '../perimeter/epoch-cards';
import { LocaleChips } from '../perimeter/locale-chips';
import { PagesChoice } from '../perimeter/pages-choice';
import { TypeChips } from '../perimeter/type-chips';
import { PatchPicker } from '../picker/patch-picker';
import { PasteBox } from '../priority/paste-box';
import { PriorityTray } from '../priority/priority-tray';
import { LaunchSummary } from '../summary/launch-summary';
import { injectAnnouncement } from './inject-announcement';
import { LaunchDialogStore } from './launch-dialog-store';

/** A step of the stepper, and the key of its name. */
interface StepLink {
  readonly step: LaunchStep;
  readonly name: 'patches' | 'scope' | 'summary';
}

const STEPS: readonly StepLink[] = [
  { step: 1, name: 'patches' },
  { step: 2, name: 'scope' },
  { step: 3, name: 'summary' },
];
// Six rows hold the place of the list while the coverage loads (§ 3.1).
const SKELETON_ROWS = [1, 2, 3, 4, 5, 6];
const LAUNCHER = 'prerender.launcher';
const FOOTER_ID = 'prerender-launch-footer';

/**
 * The launch of a pass, in three steps (§ 3): the patches, the perimeter, then the summary,
 * which is the confirmation. A stepper at the head, a footer that keeps the estimate in view
 * and carries the buttons. Thin: the state lives in its store, each step in its components.
 * Opened by `LaunchOpener`; it closes on the pass it started, or on null.
 */
@Component({
  selector: 'lodb-launch-dialog',
  imports: [
    AdminBand,
    AdminTextPipe,
    Button,
    DialogFrame,
    EpochCards,
    LaunchSummary,
    LocaleChips,
    PagesChoice,
    PasteBox,
    PatchPicker,
    PriorityTray,
    RouterLink,
    Skeleton,
    TypeChips,
  ],
  templateUrl: './launch-dialog.html',
  styles: `
    /* Sticks to the panel's padding edge, not its content edge: no row shows beneath it. */
    .launch-foot {
      position: sticky;
      inset-block-end: -1.4rem;
      z-index: 1;
      margin-block: 1.5rem -1.4rem;
      margin-inline: -1.4rem;
      padding-block: 0.9rem;
      padding-inline: 1.4rem;
      border-block-start: 1px solid color-mix(in srgb, var(--color-gold) 22%, transparent);
      background: var(--color-panel);
    }
  `,
  host: { class: 'admin-overlay block' },
  providers: [LaunchDialogStore],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LaunchDialog {
  static readonly HEADING_ID = 'prerender-launch-title';

  protected readonly store = inject(LaunchDialogStore);
  protected readonly headingId = LaunchDialog.HEADING_ID;
  protected readonly steps = STEPS;
  protected readonly skeletonRows = SKELETON_ROWS;
  protected readonly loginPath = ADMIN_PATHS.login;

  private readonly texts = injectAdminText();
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef).nativeElement;
  private readonly injector = inject(Injector);

  /** Nothing ticked at step 1: « Suivant » waits, and the footer says why. */
  protected readonly isEmpty = computed(
    () => this.store.step() === 1 && this.store.estimate()?.versions === 0,
  );
  protected readonly footerId = FOOTER_ID;
  protected readonly footer = computed(() => {
    const estimate = this.store.estimate();
    if (!estimate || this.store.patches.busy()) {
      return this.texts.text(`${LAUNCHER}.footer_loading`);
    }
    if (this.isEmpty()) {
      return this.texts.text(`${LAUNCHER}.footer_empty`);
    }
    return this.texts.text(`${LAUNCHER}.footer`, {
      ...estimate,
      duration: duration(estimate.seconds),
    });
  });
  protected readonly announced = injectAnnouncement(() => this.footer());
  protected readonly position = computed(() => {
    const { step, name } = STEPS[this.store.step() - 1];
    return this.texts.text(`${LAUNCHER}.steps.position`, {
      step,
      name: this.texts.text(`${LAUNCHER}.steps.${name}`),
    });
  });

  /** Whether the stepper may go to `step` now: back always, forward past valid steps. */
  protected canReach(step: LaunchStep): boolean {
    const validity = this.store.validity();
    return (
      step <= this.store.step() || STEPS.every((link) => link.step >= step || validity[link.step])
    );
  }

  /** The errors the API answered for `fields`, shown under their section. */
  protected errorsIn(...fields: LaunchField[]): LaunchMessage[] {
    const answered = this.store.errors()?.fields ?? {};
    return fields.flatMap((field) => answered[field] ?? []);
  }

  protected next(): void {
    this.store.goTo((this.store.step() + 1) as LaunchStep);
    this.focusForward();
  }

  protected back(): void {
    this.store.goTo((this.store.step() - 1) as LaunchStep);
    this.focusForward();
  }

  /** The units the skip switch leaves out; null while the coverage loads. */
  protected skipped(): number | null {
    return this.store.estimate()?.skipped ?? null;
  }

  // The footer button just pressed gives way to the next step's (« Lancer », « Annuler »): the
  // focus goes to the forward button, or the dialog while it is disabled, never the page.
  private focusForward(): void {
    afterNextRender(
      () => {
        const forward = this.host.querySelector<HTMLButtonElement>('[data-launch], [data-next]');
        const target = forward && !forward.disabled ? forward : this.dialogContainer();
        target?.focus();
      },
      { injector: this.injector },
    );
  }

  private dialogContainer(): HTMLElement | null {
    return this.host.closest<HTMLElement>('.cdk-dialog-container');
  }
}
