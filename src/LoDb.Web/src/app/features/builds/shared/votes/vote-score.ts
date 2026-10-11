import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  linkedSignal,
  signal,
} from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router, RouterLink } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { filter, firstValueFrom, map } from 'rxjs';
import type { VoteState } from '../../../../core/api/generated/models/vote-state';
import { BuildsService } from '../../../../core/api/generated/services/builds.service';
import { AuthSession } from '../../../../core/auth/session/auth-session';
import { RETURN_URL_PARAM } from '../../../../core/auth/guards/return-url-param';
import { PageDirection } from '../../../../core/layout/direction/page-direction';
import { ToastService } from '../../../../core/layout/toast/toast-service';
import { applyVote } from './apply-vote';
import { isVoteState } from './is-vote-state';
import type { VoteDirection } from './vote-direction';
import { voteRefusalKey } from './vote-refusal-key';

const VALUE_OF: Readonly<Record<VoteDirection, string>> = { 1: 'up', [-1]: 'down' };

/**
 * The net score of a public build and its two arrows, on its shared page and in the trends.
 * Optimistic: the score moves on the click and comes back when the API refuses the vote or
 * answers something else than a vote state; the API's answer stays authoritative. A refusal
 * the voter can act on (e-mail to verify, account too recent, too many votes) also says why
 * in a toast. A visitor who is not signed in, the server render included, gets the arrows as
 * links to sign in.
 */
@Component({
  selector: 'lodb-vote-score',
  imports: [RouterLink, TranslocoPipe],
  templateUrl: './vote-score.html',
  styleUrl: './vote-score.css',
  host: { class: 'vote-box' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class VoteScore {
  /** The id of the build voted on. */
  readonly buildId = input.required<number>();
  /** The score the page read, followed whenever the page reads it again. */
  readonly vote = input.required<VoteState>();

  private readonly builds = inject(BuildsService);
  private readonly router = inject(Router);
  private readonly session = inject(AuthSession);
  private readonly page = inject(PageDirection);
  private readonly toasts = inject(ToastService);
  private readonly transloco = inject(TranslocoService);
  private readonly url = toSignal(
    this.router.events.pipe(
      filter((event) => event instanceof NavigationEnd),
      map((event) => event.urlAfterRedirects),
    ),
    { initialValue: this.router.url },
  );

  protected readonly state = linkedSignal(() => this.vote());
  protected readonly pending = signal(false);
  protected readonly signedIn = computed(() => this.session.status() === 'authenticated');
  protected readonly loginLink = computed(() => ['/', this.page.locale(), 'account', 'login']);
  protected readonly loginQuery = computed(() => ({ [RETURN_URL_PARAM]: this.url() }));
  protected readonly scoreText = computed(() => {
    const { score } = this.state();
    return score > 0 ? `+${score}` : String(score);
  });
  protected readonly scoreClass = computed(() => {
    const { score } = this.state();
    if (score > 0) {
      return 'vote-score--positive';
    }
    return score < 0 ? 'vote-score--negative' : 'vote-score--zero';
  });

  protected async cast(direction: VoteDirection): Promise<void> {
    if (this.pending()) {
      return;
    }
    const before = this.state();
    this.state.set(applyVote(before, direction));
    this.pending.set(true);
    try {
      const body = { value: VALUE_OF[direction] };
      const answer: unknown = await firstValueFrom(
        this.builds.voteBuild({ id: this.buildId(), body }),
      );
      this.state.set(isVoteState(answer) ? answer : before);
    } catch (error) {
      this.state.set(before);
      this.explain(error);
    } finally {
      this.pending.set(false);
    }
  }

  private explain(error: unknown): void {
    const key = voteRefusalKey(error);
    if (key !== null) {
      this.toasts.show('warning', this.transloco.translate(key));
    }
  }
}
