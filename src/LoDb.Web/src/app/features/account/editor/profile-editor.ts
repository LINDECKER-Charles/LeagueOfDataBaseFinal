import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  signal,
  untracked,
} from '@angular/core';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import { ProfileService } from '../../../core/api/generated/services/profile.service';
import { AuthSession } from '../../../core/auth/session/auth-session';
import { PageDirection } from '../../../core/layout/direction/page-direction';
import { ToastService } from '../../../core/layout/toast/toast-service';
import { Button } from '../../../ui/controls/button';
import { Backdrop } from '../../../ui/surfaces/backdrop';
import { accountMessage } from '../shared/account-message';
import { overrideAccountTitle } from '../shared/head/override-account-title';
import { displayName } from '../shared/text/display-name';
import { IdentityAside } from './aside/identity-aside';
import { ProfileAutosave } from './autosave/profile-autosave';
import { ProfileCover } from './cover/profile-cover';
import { FavoritesPanel } from './favorites/favorites-panel';
import { PickerCatalog } from './picker/picker-catalog';
import { PickerOpener } from './picker/picker-opener';
import { DangerPanel } from './settings/danger-panel';
import { IdentityPanel } from './settings/identity-panel';
import { PasswordPanel } from './settings/password-panel';
import { ProfileForm } from './store/profile-form';
import { ProfileStore } from './store/profile-store';

/**
 * The profile of the signed-in account, `/{locale}/account/profile`: the favorites and the
 * visibility, saved as they change; the identity, the first password and the erasure, each
 * sent by its own form. Pinning the favorites to a version reads them again on that patch.
 */
@Component({
  selector: 'lodb-profile-editor',
  imports: [
    Backdrop,
    Button,
    DangerPanel,
    FavoritesPanel,
    IdentityAside,
    IdentityPanel,
    PasswordPanel,
    ProfileCover,
    TranslocoPipe,
  ],
  templateUrl: './profile-editor.html',
  styleUrl: './profile-editor.css',
  providers: [PickerCatalog, PickerOpener, ProfileAutosave, ProfileForm, ProfileStore],
  host: { class: 'block' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProfileEditor {
  private readonly store = inject(ProfileStore);
  private readonly catalog = inject(PickerCatalog);
  private readonly autosave = inject(ProfileAutosave);
  private readonly profiles = inject(ProfileService);
  private readonly session = inject(AuthSession);
  private readonly page = inject(PageDirection);
  private readonly toasts = inject(ToastService);
  private readonly transloco = inject(TranslocoService);
  protected readonly form = inject(ProfileForm);

  protected readonly status = computed(() => this.store.state().status);
  protected readonly profile = computed(() => {
    const state = this.store.state();
    return state.status === 'ready' ? state.profile : null;
  });
  protected readonly versions = computed(() => {
    const state = this.store.state();
    return state.status === 'ready' ? state.versions : [];
  });
  protected readonly name = computed(() => {
    const profile = this.profile();
    return profile === null ? '' : displayName(profile.username, profile.riotTagline);
  });
  protected readonly pinning = signal(false);

  constructor() {
    // The tab reads the summoner's name, as the legacy editor's did.
    overrideAccountTitle(() => (this.profile() === null ? null : { text: this.name() }));
    // Each reading of the profile is the new starting point of what the editor changes.
    effect(() => {
      const profile = this.profile();
      if (profile !== null) {
        untracked(() => {
          this.form.load(profile);
          this.catalog.use(profile.showcase.version, profile.showcase.language);
        });
      }
    });
    void this.store.load();
  }

  protected retry(): void {
    void this.store.load();
  }

  protected async pin(version: string | null): Promise<void> {
    this.pinning.set(true);
    try {
      await this.autosave.flush();
      await firstValueFrom(this.profiles.setPreferredVersion({ body: { version } }));
      await this.reload();
    } catch {
      await this.warn('account.editor.version_error');
    } finally {
      this.pinning.set(false);
    }
  }

  /** Reads the profile again once what waits is saved; the session learns a new name too. */
  protected async reload(): Promise<void> {
    await this.autosave.flush();
    await this.session.refresh().catch(() => null);
    if (!(await this.store.load())) {
      await this.warn('account.editor.load_error');
    }
  }

  protected fallBack(art: HTMLImageElement, fallback: string): void {
    if (art.getAttribute('src') !== fallback) {
      art.src = fallback;
    }
  }

  private async warn(key: string): Promise<void> {
    this.toasts.show('error', await accountMessage(this.transloco, this.page.locale(), key));
  }
}
