import { ChangeDetectionStrategy, Component } from '@angular/core';
import type { PublicProfile } from '../../core/api/generated/models/public-profile';
import { injectRouteData } from '../../core/routing/inject-route-data';
import { ProfileBuilds } from './card/profile-builds';
import { ProfileHero } from './card/profile-hero';
import { applyProfileHead } from './head/apply-profile-head';

/**
 * The public profile, `/{locale}/u/{username}`: the summoner card its owner chose to show,
 * its favorites over the skin's splash and the builds it published, resolved by the route
 * (`resolvePublicProfile`) and rendered on the server without an account, for the visitors
 * and the search engines alike.
 */
@Component({
  selector: 'lodb-profile-page',
  imports: [ProfileBuilds, ProfileHero],
  template: `
    @let card = profile();
    <lodb-profile-hero [profile]="card" />
    <lodb-profile-builds [builds]="card.builds" />
  `,
  host: { class: 'block' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProfilePage {
  protected readonly profile = injectRouteData<PublicProfile>('profile');

  constructor() {
    applyProfileHead(this.profile);
  }
}
