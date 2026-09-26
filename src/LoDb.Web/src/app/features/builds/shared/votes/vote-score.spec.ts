import { HttpStatusCode, provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Component, signal } from '@angular/core';
import { type ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideTransloco } from '@jsverse/transloco';
import { of } from 'rxjs';
import type { VoteState } from '../../../../core/api/generated/models/vote-state';
import { API_BASE_URL } from '../../../../core/api/api-base-url';
import { AuthSession } from '../../../../core/auth/session/auth-session';
import type { SessionStatus } from '../../../../core/auth/session/session-status';
import { VoteScore } from './vote-score';

const VOTE_URL = '/api/builds/7/vote';
const LABELS = {
  community: {
    vote: { up: 'Upvote', down: 'Downvote', score_label: 'Score', login: 'Sign in to vote' },
  },
};

@Component({
  imports: [VoteScore],
  template: `<lodb-vote-score [buildId]="7" [vote]="vote()" />`,
})
class Host {
  readonly vote = signal<VoteState>({ score: 3, myVote: 0 });
}

function mount(vote: VoteState, status: SessionStatus = 'authenticated') {
  TestBed.configureTestingModule({
    providers: [
      provideRouter([]),
      provideHttpClient(),
      provideHttpClientTesting(),
      { provide: API_BASE_URL, useValue: '' },
      { provide: AuthSession, useValue: { status: signal(status) } },
      provideTransloco({
        config: { availableLangs: ['en'], defaultLang: 'en', prodMode: true },
        loader: class {
          getTranslation = () => of(LABELS);
        },
      }),
    ],
  });
  const fixture = TestBed.createComponent(Host);
  fixture.componentInstance.vote.set(vote);
  fixture.detectChanges();
  return { fixture, http: TestBed.inject(HttpTestingController) };
}

function scoreOf(fixture: ComponentFixture<Host>): string {
  fixture.detectChanges();
  return (fixture.nativeElement as HTMLElement).querySelector('.vote-score')?.textContent ?? '';
}

function arrow(fixture: ComponentFixture<Host>, index: 0 | 1): HTMLButtonElement {
  const buttons = (fixture.nativeElement as HTMLElement).querySelectorAll('button');
  return buttons[index];
}

async function settle(fixture: ComponentFixture<Host>): Promise<void> {
  await fixture.whenStable();
  fixture.detectChanges();
}

// The cases of the legacy spec (assets/vue/components/community/VoteScore.spec.ts).
describe('VoteScore', () => {
  it('shows the signed net score', () => {
    expect(scoreOf(mount({ score: 3, myVote: 0 }).fixture)).toBe('+3');
    TestBed.resetTestingModule();
    expect(scoreOf(mount({ score: -2, myVote: 0 }).fixture)).toBe('-2');
    TestBed.resetTestingModule();
    expect(scoreOf(mount({ score: 0, myVote: 0 }).fixture)).toBe('0');
  });

  it('bumps the score at once, then keeps the value the API answers', async () => {
    const { fixture, http } = mount({ score: 3, myVote: 0 });

    arrow(fixture, 0).click();
    expect(scoreOf(fixture)).toBe('+4');
    const request = http.expectOne(VOTE_URL);
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ value: 'up' });
    request.flush({ score: 4, myVote: 1 });
    await settle(fixture);

    expect(scoreOf(fixture)).toBe('+4');
    expect(arrow(fixture, 0).getAttribute('aria-pressed')).toBe('true');
  });

  it('rolls the optimistic move back when the API refuses the vote', async () => {
    const { fixture, http } = mount({ score: 3, myVote: 0 });

    arrow(fixture, 1).click();
    expect(scoreOf(fixture)).toBe('+2');
    expect(arrow(fixture, 1).disabled).toBe(true);
    const request = http.expectOne(VOTE_URL);
    expect(request.request.body).toEqual({ value: 'down' });
    request.flush({ status: 403 }, { status: HttpStatusCode.Forbidden, statusText: 'Forbidden' });
    await settle(fixture);

    expect(scoreOf(fixture)).toBe('+3');
    expect(arrow(fixture, 1).getAttribute('aria-pressed')).toBe('false');
    expect(arrow(fixture, 1).disabled).toBe(false);
  });

  it('rolls back on an answer that is not a vote state', async () => {
    const { fixture, http } = mount({ score: 1, myVote: 1 });

    arrow(fixture, 0).click();
    http.expectOne(VOTE_URL).flush({ score: 'many', myVote: 9 });
    await settle(fixture);

    expect(scoreOf(fixture)).toBe('+1');
  });

  it('lights up the reader’s vote and follows an answer that withdraws it', async () => {
    const { fixture, http } = mount({ score: 1, myVote: 1 });
    expect(arrow(fixture, 0).classList).toContain('vote-arrow--on-up');

    arrow(fixture, 0).click();
    http.expectOne(VOTE_URL).flush({ score: 0, myVote: 0 });
    await settle(fixture);

    expect(scoreOf(fixture)).toBe('0');
    expect(arrow(fixture, 0).classList).not.toContain('vote-arrow--on-up');
  });

  it('sends one vote at a time', () => {
    const { fixture, http } = mount({ score: 3, myVote: 0 });

    arrow(fixture, 0).click();
    fixture.detectChanges();
    arrow(fixture, 1).click();

    expect(http.match(VOTE_URL)).toHaveLength(1);
  });

  it('follows the score the page reads again, such as the reader’s own vote', () => {
    const { fixture } = mount({ score: 3, myVote: 0 });

    fixture.componentInstance.vote.set({ score: 3, myVote: -1 });

    expect(scoreOf(fixture)).toBe('+3');
    expect(arrow(fixture, 1).classList).toContain('vote-arrow--on-down');
  });

  it.each<SessionStatus>(['anonymous', 'unknown'])(
    'offers links to sign in, back to the page, to a visitor %s',
    (status) => {
      const { fixture } = mount({ score: 3, myVote: 0 }, status);
      const host = fixture.nativeElement as HTMLElement;
      const links = [...host.querySelectorAll('a')];

      expect(host.querySelectorAll('button')).toHaveLength(0);
      expect(links).toHaveLength(2);
      expect(links[0].getAttribute('href')).toBe('/en/account/login?returnUrl=%2F');
      expect(links[0].getAttribute('title')).toBe('Sign in to vote');
    },
  );
});
