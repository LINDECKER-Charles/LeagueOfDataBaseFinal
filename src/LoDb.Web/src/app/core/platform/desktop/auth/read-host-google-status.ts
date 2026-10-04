import type { HostGoogleStatus } from './host-google-status';

// A failure without a readable code, or an idle host holding no session.
const UNKNOWN_FAILURE = 'google-failed';

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null;
}

/**
 * Reads the host's session as untrusted: `idle` only counts as a success when the host also
 * holds a session, anything unreadable counts as still pending until the deadline.
 */
export function readHostGoogleStatus(session: unknown): HostGoogleStatus {
  if (!isRecord(session)) {
    return { stage: 'pending' };
  }
  const failure = session['googleFailure'];
  switch (session['google']) {
    case 'idle':
      return session['signedIn'] === true
        ? { stage: 'succeeded' }
        : { stage: 'failed', failure: UNKNOWN_FAILURE };
    case 'failed':
      return { stage: 'failed', failure: typeof failure === 'string' ? failure : UNKNOWN_FAILURE };
    default:
      return { stage: 'pending' };
  }
}
