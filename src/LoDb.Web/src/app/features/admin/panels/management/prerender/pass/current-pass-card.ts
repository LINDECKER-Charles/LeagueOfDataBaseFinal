import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import type { PrerenderPassView } from '../../../../../../core/api/generated/models/prerender-pass-view';
import { Button } from '../../../../../../ui/controls/button';
import { duration } from '../../../../format/duration';
import { FigurePipe } from '../../../../format/figure-pipe';
import { StampPipe } from '../../../../format/stamp-pipe';
import { AdminCard } from '../../../../layout/admin-card';
import { AdminTextPipe } from '../../../../shared/admin-text-pipe';
import { injectAdminText } from '../../../../shared/inject-admin-text';
import { Badge } from '../../../../widgets/badge';
import { ConfirmButton } from '../../../../widgets/confirm-button';
import { DecimalBytesPipe } from '../format/decimal-bytes-pipe';
import { prerenderTone } from '../prerender-tone';
import { scopeSummary } from './scope-summary';
import { unitShare } from './unit-share';

const RUNNING = 'running';
const PAUSED = 'paused';

/**
 * The active pass of the pre-rendering, running or paused: how far its units went, its pages
 * by outcome, its pace and when it should end at that pace, what it wrote, who launched it
 * and over what. A running pass pauses, a paused one resumes, and either cancels once
 * confirmed; the panel runs the action.
 */
@Component({
  selector: 'lodb-current-pass-card',
  imports: [
    AdminCard,
    AdminTextPipe,
    Badge,
    Button,
    ConfirmButton,
    DecimalBytesPipe,
    FigurePipe,
    StampPipe,
  ],
  templateUrl: './current-pass-card.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CurrentPassCard {
  readonly pass = input.required<PrerenderPassView>();
  /** Whether an action of the panel is in flight. */
  readonly busy = input(false);
  readonly paused = output<void>();
  readonly resumed = output<void>();
  readonly cancelled = output<void>();

  private readonly texts = injectAdminText();

  protected readonly isRunning = computed(() => this.pass().state === RUNNING);
  protected readonly isPaused = computed(() => this.pass().state === PAUSED);
  protected readonly tone = computed(() => prerenderTone(this.pass().state));
  protected readonly share = computed(() => unitShare(this.pass().units));
  protected readonly pagesDone = computed(() => {
    const pass = this.pass();
    return pass.pagesWritten + pass.pagesSkipped + pass.pagesFailed;
  });
  protected readonly runningTime = computed(() => duration(this.pass().activeSeconds));
  protected readonly scope = computed(() => scopeSummary(this.pass().scope, this.texts));
}
