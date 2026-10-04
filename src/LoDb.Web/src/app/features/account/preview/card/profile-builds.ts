import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import type { ProfileBuildCard } from '../../../../core/api/generated/models/profile-build-card';
import { Image } from '../../../../ui/media/image';
import { imageSource } from '../../editor/entries/image-source';
import { initialsOf } from '../../editor/entries/initials-of';

/**
 * The builds a public card lists, the latest updated first, each a link to its shared page,
 * `/b/{token}`, which lives outside the locales. The public page has its twin.
 */
@Component({
  selector: 'lodb-profile-builds',
  imports: [Image, RouterLink, TranslocoPipe],
  templateUrl: './profile-builds.html',
  styleUrl: './builds.css',
  host: { class: 'profile-builds' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProfileBuilds {
  readonly builds = input.required<readonly ProfileBuildCard[]>();

  protected readonly cards = computed(() =>
    this.builds().map((build) => {
      // A champion the card's patch lacks is named by its id.
      const champion = build.championName ?? build.championId;
      return {
        token: build.shareToken,
        name: build.name,
        portrait: imageSource(build.championImage),
        initials: initialsOf(champion),
        meta: `${champion} · ${build.gameVersion}`,
      };
    }),
  );
}
