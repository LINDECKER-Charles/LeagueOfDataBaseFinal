import type { UpdateState } from '../../update-state';

const ORDER: readonly UpdateState[] = ['none', 'downloading', 'ready'];

/** The state of the app: the most advanced of its update sources. */
export function mostAdvancedState(states: readonly UpdateState[]): UpdateState {
  return states.reduce(
    (most, state) => (ORDER.indexOf(state) > ORDER.indexOf(most) ? state : most),
    'none',
  );
}
