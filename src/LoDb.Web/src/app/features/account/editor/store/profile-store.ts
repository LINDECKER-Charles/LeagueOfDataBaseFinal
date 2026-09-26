import { Injectable, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { ApiMeta } from '../../../../core/api/meta/api-meta';
import { ProfileService } from '../../../../core/api/generated/services/profile.service';
import { injectCatalogQuery } from '../../shared/inject-catalog-query';
import type { ProfileState } from './profile-state';

/**
 * The profile of the signed-in account, its favorites resolved in the context of the page
 * unless it pins a version, and the versions it may pin. Loaded again when the pin changes,
 * since the favorites then resolve on another patch.
 */
@Injectable()
export class ProfileStore {
  private readonly profiles = inject(ProfileService);
  private readonly meta = inject(ApiMeta);
  private readonly catalogQuery = injectCatalogQuery();
  private readonly current = signal<ProfileState>({ status: 'loading' });

  readonly state = this.current.asReadonly();

  /** Reads the profile; says whether it could. A profile already shown stays on a failure. */
  async load(): Promise<boolean> {
    try {
      const [profile, meta] = await Promise.all([
        firstValueFrom(this.profiles.getOwnProfile(await this.catalogQuery())),
        firstValueFrom(this.meta.meta()),
      ]);
      this.current.set({ status: 'ready', profile, versions: meta.versions });
      return true;
    } catch {
      if (this.current().status !== 'ready') {
        this.current.set({ status: 'failed' });
      }
      return false;
    }
  }
}
