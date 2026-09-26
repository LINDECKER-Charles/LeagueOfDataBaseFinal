import type { UpdateState } from '../../update-state';

const STATES: readonly UpdateState[] = ['none', 'downloading', 'ready'];

/** The `state` of an `updateState` result, or null when the host answered something else. */
export function updateStateOf(result: Readonly<Record<string, unknown>>): UpdateState | null {
  const state = result['state'];
  return STATES.find((known) => known === state) ?? null;
}
