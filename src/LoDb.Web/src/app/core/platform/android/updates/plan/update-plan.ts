import type { LiveStep } from './live-step';
import type { NativeStep } from './native-step';

/** The two levels of the Android update (ADR 0008), decided together by `planUpdate`. */
export interface UpdatePlan {
  readonly native: NativeStep;
  readonly live: LiveStep;
}
