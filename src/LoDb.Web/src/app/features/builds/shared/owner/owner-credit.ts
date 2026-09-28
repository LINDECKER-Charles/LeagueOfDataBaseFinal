import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import type { OwnerView } from '../../../../core/api/generated/models/owner-view';
import { PageDirection } from '../../../../core/layout/direction/page-direction';
import { displayName } from './display-name';
import { SupporterBadge } from './supporter-badge';

/**
 * "By {name}", the author of a build: a link to their public card when they have one, in
 * the page's locale (the URL keeps the bare username, a tag's # cannot live in a path), and
 * the supporter seal when they support the site.
 */
@Component({
  selector: 'lodb-owner-credit',
  imports: [RouterLink, SupporterBadge, TranslocoPipe],
  template: `
    @let author = owner();
    @let credit = 'build.show.by' | transloco: { name: name() };
    @if (author.hasPublicProfile) {
      <a class="nav-link text-gold" [routerLink]="profileLink()">{{ credit }}</a>
    } @else {
      <span>{{ credit }}</span>
    }
    @if (author.isSupporter) {
      <lodb-supporter-badge />
    }
  `,
  host: { class: 'inline-flex min-w-0 items-center gap-1.5' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class OwnerCredit {
  readonly owner = input.required<OwnerView>();

  private readonly page = inject(PageDirection);

  protected readonly name = computed(() => {
    const { username, riotTagline } = this.owner();
    return displayName(username, riotTagline);
  });
  protected readonly profileLink = computed(() => [
    '/',
    this.page.locale(),
    'u',
    this.owner().username,
  ]);
}
