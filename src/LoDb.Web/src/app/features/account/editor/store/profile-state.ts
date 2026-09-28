import type { OwnerProfile } from '../../../../core/api/generated/models/owner-profile';

/** Where the profile of the editor stands: on its way, unreadable, or there. */
export type ProfileState =
  | { readonly status: 'loading' }
  | { readonly status: 'failed' }
  | {
      readonly status: 'ready';
      readonly profile: OwnerProfile;
      /** The versions the favorites may be pinned to, newest first. */
      readonly versions: readonly string[];
    };
