import { ChangeDetectionStrategy, Component } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';

/**
 * Provisional public profile, `/{locale}/u/{username}` (L3.1). The profile chantier (L4.6)
 * replaces it and keeps profile.routes.ts.
 */
@Component({
  selector: 'lodb-profile-page',
  imports: [TranslocoPipe],
  template: `<div class="mx-auto max-w-6xl px-4 py-10 sm:px-6">
    <h1 class="font-beaufort text-3xl tracking-wide text-gold-grad uppercase">
      {{ 'profile.public.eyebrow' | transloco }}
    </h1>
  </div>`,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProfilePage {}
