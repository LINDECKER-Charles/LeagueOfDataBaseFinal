import { DIALOG_DATA, DialogRef } from '@angular/cdk/dialog';
import { Injectable, computed, effect, inject, signal, untracked } from '@angular/core';
import type { PrerenderPassStarted } from '../../../../../../../core/api/generated/models/prerender-pass-started';
import type { PrerenderPassView } from '../../../../../../../core/api/generated/models/prerender-pass-view';
import type { PrerenderReport } from '../../../../../../../core/api/generated/models/prerender-report';
import { injectPatches } from '../../common/scope/inject-patches';
import type { LaunchPreset } from '../../common/scope/launch-preset';
import { type SelectionAction, selectionAfter } from '../../common/selection/patch-selection';
import { SelectionUndo } from '../../common/selection/selection-undo';
import { injectLaunchCall } from '../call/inject-launch-call';
import { type LaunchErrors, type LaunchStep, launchErrorsOf } from '../call/launch-errors-of';
import { requestOfDraft } from '../call/request-of-draft';
import { type EpochChoice, epochChoices } from '../perimeter/epoch-choices';
import type { PastedVersions } from '../priority/paste-box';
import { launchEstimate } from '../summary/launch-estimate';
import { launchWarnings } from '../summary/launch-warnings';
import { type LaunchDraft, draftOf } from './launch-draft';

/** What the page opens the launch dialog with. */
export interface LaunchData {
  readonly preset: LaunchPreset;
  /** The report the page read last: the epochs, and the passes known at opening. */
  readonly report: PrerenderReport;
}

// Where each entry lands (§ 3.1): a prefilled scope goes straight to what it changes.
const FIRST_STEPS: Readonly<Record<LaunchPreset['origin'], LaunchStep>> = {
  header: 1,
  idle: 1,
  coverage: 2,
  'complete-epoch': 3,
  relaunch: 3,
  failures: 3,
};
// The bands that forbid a launch until the dialog closes.
const LOCKING_BANDS: readonly LaunchErrors['band'][] = ['active', 'dormant'];

// A pass « Vérifier » found, told as its launch would have: what was skipped is not known.
function startedOf(pass: PrerenderPassView): PrerenderPassStarted {
  return {
    id: pass.id,
    epoch: pass.epoch,
    units: pass.units.total,
    skippedUnits: 0,
    ignoredVersions: [],
  };
}

/**
 * The state of the launch dialog (§ 3): the step, the draft, the coverage of the epoch it
 * writes (read again when the epoch changes), the estimate and its warnings, the errors of
 * the API, and the call itself. Provided by the dialog, which only renders it.
 */
@Injectable()
export class LaunchDialogStore {
  private readonly data = inject<LaunchData>(DIALOG_DATA);
  private readonly ref = inject<DialogRef<PrerenderPassStarted | null>>(DialogRef);
  private readonly call = injectLaunchCall();
  // The newest pass known at opening: a newer one « Vérifier » finds is the one launched.
  // Server ids, never the clock of the browser against `createdAt`.
  private readonly knownPassId = Math.max(0, ...this.data.report.passes.map((pass) => pass.id));

  readonly report = this.data.report;
  readonly choices: readonly EpochChoice[] = epochChoices(this.report);
  readonly step = signal<LaunchStep>(FIRST_STEPS[this.data.preset.origin]);
  readonly draft = signal<LaunchDraft>(draftOf(this.data.preset, this.report, null));
  // Its own signal: the coverage is read again when the epoch changes, not at each tick.
  private readonly epoch = computed(() => this.draft().epoch);
  readonly patches = injectPatches(this.epoch);
  readonly undo = signal(SelectionUndo.empty());
  readonly errors = signal<LaunchErrors | null>(null);
  readonly isBusy = this.call.isBusy;
  /** The coverage of the epoch written, once read for that epoch. */
  readonly coverage = computed(() => {
    const patches = this.patches.value();
    return patches?.epoch === this.draft().epoch ? patches : null;
  });
  readonly estimate = computed(() => {
    const coverage = this.coverage();
    return coverage ? launchEstimate(this.draft(), coverage) : null;
  });
  readonly warnings = computed(() => {
    const estimate = this.estimate();
    return estimate ? launchWarnings(this.draft(), estimate, this.report) : [];
  });
  /** Whether each step lets the operator go on: a patch; a locale and a type; a unit. */
  readonly validity = computed<Readonly<Record<LaunchStep, boolean>>>(() => {
    const draft = this.draft();
    return {
      1: draft.isResolved && draft.selection.selected.size > 0,
      2: draft.locales.length > 0 && draft.pageTypes.length > 0,
      3: (this.estimate()?.units ?? 0) > 0,
    };
  });
  readonly canLaunch = computed(() => {
    const isValid = Object.values(this.validity()).every(Boolean);
    const isLocked = LOCKING_BANDS.includes(this.errors()?.band ?? null);
    return isValid && !isLocked && !this.isBusy();
  });

  constructor() {
    // The ticks of 'all', 'incomplete' and the lists wait for the coverage (draftOf).
    effect(() => {
      const coverage = this.coverage();
      if (coverage && !untracked(this.draft).isResolved) {
        const { selection } = draftOf(this.data.preset, this.report, coverage);
        this.draft.update((draft) => ({ ...draft, selection, isResolved: true }));
      }
    });
  }

  /** Sends `action` to the selection, keeping the state to undo it. */
  act(action: SelectionAction): void {
    const { selection } = this.draft();
    this.undo.update((undo) => undo.after(selection, action));
    this.update({ selection: selectionAfter(selection, action) });
  }

  /** Applies a pasted list: ticked, rendered first; past 100, the summary says so. */
  paste(pasted: PastedVersions): void {
    this.act({ kind: 'paste', versions: pasted.versions, append: pasted.append });
    this.update({ isPriorityCapped: pasted.isCapped });
  }

  undoLast(): void {
    const step = this.undo().undo();
    if (step) {
      this.undo.set(step.rest);
      this.update({ selection: step.selection });
    }
  }

  /** Changes the draft; the errors of its fields no longer hold. */
  update(changes: Partial<LaunchDraft>): void {
    this.draft.update((draft) => ({ ...draft, ...changes }));
    if (this.errors()?.band === null) {
      this.errors.set(null);
    }
  }

  chooseEpoch(choice: EpochChoice): void {
    this.update({ epoch: choice.epoch, epochKind: choice.kind });
  }

  /** Goes back at any time, forward only past steps that are valid. */
  goTo(step: LaunchStep): void {
    const validity = this.validity();
    const isOpen = ([1, 2] as const).every((before) => before >= step || validity[before]);
    if (step <= this.step() || isOpen) {
      this.step.set(step);
    }
  }

  /** « changer d'epoch »: the perimeter, whatever the patches ticked so far. */
  changeEpoch(): void {
    this.step.set(2);
  }

  async launch(): Promise<void> {
    const coverage = this.coverage();
    if (!coverage || !this.canLaunch()) {
      return;
    }
    this.errors.set(null);
    const outcome = await this.call.start(requestOfDraft(this.draft(), coverage));
    if (outcome.started) {
      this.ref.close(outcome.started);
      return;
    }
    const errors = launchErrorsOf(outcome.problem);
    this.errors.set(errors);
    if (errors.step !== null) {
      this.step.set(errors.step);
    }
  }

  /** « Vérifier », after no answer: closes on a newer active pass, else launches again. */
  async check(): Promise<void> {
    const report = await this.call.check();
    const pass = report?.current;
    if (pass && pass.id > this.knownPassId) {
      this.ref.close(startedOf(pass));
    } else if (report) {
      this.errors.set(null);
    }
  }

  /** Closes without a pass: « Annuler », or « Fermer et suivre la passe » after a conflict. */
  close(): void {
    this.ref.close(null);
  }
}
