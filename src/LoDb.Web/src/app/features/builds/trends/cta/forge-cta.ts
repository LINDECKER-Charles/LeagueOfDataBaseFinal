import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { AuthSession } from '../../../../core/auth/session/auth-session';
import type { Locale } from '../../../../core/i18n/locales';
import { Button } from '../../../../ui/controls/button';

/**
 * The call to forge a build, the interaction that feeds the ranking: the editor for a signed-in
 * reader, the sign-up for anyone else, as the editor wants a verified account. The server
 * renders it anonymous (ADR 0005); the browser turns it to the editor once signed in.
 */
@Component({
  selector: 'lodb-forge-cta',
  imports: [Button, RouterLink, TranslocoPipe],
  template: `<a lodbButton="gold" [routerLink]="target().link">{{
    target().label | transloco
  }}</a>`,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ForgeCta {
  readonly locale = input.required<Locale>();

  private readonly session = inject(AuthSession);

  protected readonly target = computed(() =>
    this.session.status() === 'authenticated'
      ? {
          link: ['/', this.locale(), 'account', 'builds', 'new'],
          label: 'community.trends.empty_cta',
        }
      : { link: ['/', this.locale(), 'account', 'register'], label: 'nav.register' },
  );
}
