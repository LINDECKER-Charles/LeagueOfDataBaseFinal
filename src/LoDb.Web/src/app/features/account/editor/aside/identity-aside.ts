import { ChangeDetectionStrategy, Component, computed, inject, input, output } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslocoPipe, provideTranslocoScope } from '@jsverse/transloco';
import { PageDirection } from '../../../../core/layout/direction/page-direction';
import { localePath } from '../../../../core/layout/shell/locale-path';
import { memberSince } from '../../shared/text/member-since';
import { VisibilityCard } from './visibility-card';

// The API portal names its own link: its texts are the `api` scope's.
const API_SCOPE = 'api';

/**
 * The side column of the editor: the masked e-mail and the date of joining, the visibility
 * of the public card, and the ways to the builds and to the API key.
 */
@Component({
  selector: 'lodb-identity-aside',
  imports: [RouterLink, TranslocoPipe, VisibilityCard],
  templateUrl: './identity-aside.html',
  styleUrl: './aside.css',
  providers: [provideTranslocoScope(API_SCOPE)],
  host: { class: 'profile-identity' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class IdentityAside {
  private readonly page = inject(PageDirection);

  readonly maskedEmail = input.required<string>();
  /** When the account was created, as the API dates it. */
  readonly createdAt = input.required<string>();
  readonly username = input.required<string>();
  readonly isPublic = input.required<boolean>();
  readonly visibility = output<boolean>();

  protected readonly since = computed(() => memberSince(this.createdAt(), this.page.locale()));
  protected readonly link = (path: string) => localePath(this.page.locale(), path);
}
