import {
  ChangeDetectionStrategy,
  Component,
  Injector,
  computed,
  inject,
  signal,
} from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { injectAuthSession } from '../shared/inject-auth-session';
import { ResendVerification } from '../shared/resend-verification';

/**
 * The strip over every page of a signed-in account whose e-mail is not verified yet, with a
 * button that sends the link again (`banner` slot). Nothing for anyone else, nor on the
 * server, which never knows the session.
 */
@Component({
  selector: 'lodb-verify-email-banner',
  imports: [TranslocoPipe],
  template: `@if (unverified()) {
    <div
      class="border-b border-gold-deep bg-panel px-4 py-2.5 text-center text-sm text-gold-bright"
      role="region"
      [attr.aria-label]="'auth.verify.banner' | transloco"
    >
      {{ 'auth.verify.banner' | transloco }}
      <button
        type="button"
        class="nav-link ms-2 font-beaufort tracking-[0.12em] text-hex uppercase underline underline-offset-4"
        [disabled]="sending()"
        (click)="resend()"
      >
        {{ 'auth.verify.banner_cta' | transloco }}
      </button>
    </div>
  }`,
  host: { class: 'block' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class VerifyEmailBanner {
  private readonly session = injectAuthSession();
  private readonly injector = inject(Injector);
  protected readonly sending = signal(false);
  protected readonly unverified = computed(() => this.session?.user()?.emailVerified === false);

  // Every page carries the banner: what it sends with is only built when it is used.
  protected async resend(): Promise<void> {
    this.sending.set(true);
    await this.injector.get(ResendVerification).send();
    this.sending.set(false);
  }
}
