import { FlexibleUpdateInstallStatus } from '@capawesome/capacitor-app-update';
import type { UpdateState } from '../../../update-state';

const DOWNLOADING = new Set<FlexibleUpdateInstallStatus | undefined>([
  FlexibleUpdateInstallStatus.PENDING,
  FlexibleUpdateInstallStatus.DOWNLOADING,
  FlexibleUpdateInstallStatus.INSTALLING,
]);

/**
 * Where a flexible Play update stands, in the words of `core/update`: `ready` once Play has
 * downloaded it, which a restart installs. Failed, canceled and unknown updates show nothing.
 */
export function installStateOf(status: FlexibleUpdateInstallStatus | undefined): UpdateState {
  if (status === FlexibleUpdateInstallStatus.DOWNLOADED) {
    return 'ready';
  }
  return DOWNLOADING.has(status) ? 'downloading' : 'none';
}
