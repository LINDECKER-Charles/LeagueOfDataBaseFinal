import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import type { PublicProfile } from '../../../core/api/generated/models/public-profile';
import { ProfileService } from '../../../core/api/generated/services/profile.service';
import { PageDirection } from '../../../core/layout/direction/page-direction';
import { localePath } from '../../../core/layout/shell/locale-path';
import { Button } from '../../../ui/controls/button';
import { injectCatalogQuery } from '../shared/inject-catalog-query';
import { ProfileBuilds } from './card/profile-builds';
import { ProfileHero } from './card/profile-hero';

type PreviewState =
  | { readonly status: 'loading' }
  | { readonly status: 'failed' }
  | { readonly status: 'ready'; readonly profile: PublicProfile };

/**
 * The owner's own public card, `/{locale}/account/profile/preview`, exactly as a visitor
 * reads it at `/{locale}/u/{username}`, even while the profile is private: a ribbon above it
 * says which, and leads back to the editor.
 */
@Component({
  selector: 'lodb-profile-preview',
  imports: [Button, ProfileBuilds, ProfileHero, RouterLink, TranslocoPipe],
  templateUrl: './profile-preview.html',
  styleUrl: './preview.css',
  host: { class: 'block' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProfilePreview {
  private readonly profiles = inject(ProfileService);
  private readonly catalogQuery = injectCatalogQuery();
  private readonly page = inject(PageDirection);

  protected readonly state = signal<PreviewState>({ status: 'loading' });
  protected readonly profile = computed(() => {
    const state = this.state();
    return state.status === 'ready' ? state.profile : null;
  });
  protected readonly editor = computed(() => localePath(this.page.locale(), 'account/profile'));

  constructor() {
    void this.load();
  }

  protected async load(): Promise<void> {
    this.state.set({ status: 'loading' });
    try {
      const query = await this.catalogQuery();
      const profile = await firstValueFrom(this.profiles.previewOwnProfile(query));
      this.state.set({ status: 'ready', profile });
    } catch {
      this.state.set({ status: 'failed' });
    }
  }
}
