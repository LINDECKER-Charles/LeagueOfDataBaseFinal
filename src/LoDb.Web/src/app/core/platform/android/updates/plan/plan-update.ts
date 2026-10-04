import type { LiveUpdateBundle } from '../../../../api/generated/models/live-update-bundle';
import { isBelow } from './is-below';
import type { LiveStep } from './live-step';
import type { NativeStep } from './native-step';
import type { UpdateFacts } from './update-facts';
import type { UpdatePlan } from './update-plan';
import { usableBundleOf } from './usable-bundle';

const KEEP: LiveStep = { kind: 'keep' };

function nativeStepOf({ nativeVersion, policy }: UpdateFacts): NativeStep {
  if (policy.minimumVersion && isBelow(nativeVersion, policy.minimumVersion)) {
    return 'immediate';
  }
  if (policy.latestVersion && isBelow(nativeVersion, policy.latestVersion)) {
    return 'flexible';
  }
  return 'none';
}

// The bundle this shell must run: the policy's, unless it is malformed, needs a newer shell,
// or already failed to start here. Otherwise the one embedded in the APK (null), which passed
// the release gate with this very shell.
function targetOf(facts: UpdateFacts): LiveUpdateBundle | null {
  const bundle = usableBundleOf(facts.policy.bundle);
  if (bundle === null || facts.rejectedBundleIds.includes(bundle.id)) {
    return null;
  }
  return isBelow(facts.nativeVersion, bundle.minimumNativeVersion) === false ? bundle : null;
}

function liveStepOf(facts: UpdateFacts): LiveStep {
  const target = targetOf(facts);
  if (facts.nextBundleId === (target?.id ?? null)) {
    return KEEP;
  }
  if (target === null) {
    return { kind: 'reset' };
  }
  return facts.downloadedBundleIds.includes(target.id)
    ? { kind: 'activate', bundleId: target.id }
    : { kind: 'download', bundle: target };
}

/**
 * The update decision of the Android app (ADR 0008), a pure function of the installed shell,
 * the bundles on the device and the client policy: the API decides, and withdrawing a
 * faulty bundle is publishing the policy again. A shell below the minimum only updates
 * itself: no bundle can lift a 426, which judges the native version.
 */
export function planUpdate(facts: UpdateFacts): UpdatePlan {
  const native = nativeStepOf(facts);
  return { native, live: native === 'immediate' ? KEEP : liveStepOf(facts) };
}
