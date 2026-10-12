import type { Tone } from '../../../widgets/badge';

// The states of a database's backups as the API names them, and the outcomes of a night.
const TONES: Readonly<Record<string, Tone>> = {
  ok: 'good',
  late: 'warn',
  recovered: 'warn',
  failed: 'bad',
  none: 'muted',
};

/**
 * The tone of a state of the backups. One the admin does not know reads as a failure, as a
 * probe does: a backup that answers something unexpected is not one to trust.
 */
export function backupTone(state: string): Tone {
  return TONES[state] ?? 'bad';
}
