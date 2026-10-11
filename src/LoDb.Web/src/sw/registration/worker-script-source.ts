import { WORKER_SCRIPT_URL } from './worker-script-url';

// The one policy of its own the pages' Trusted Types admit (src/server/security/csp).
const POLICY_NAME = 'lodb#worker';

interface ScriptUrlPolicy {
  createScriptURL(url: string): unknown;
}

interface ScriptUrlPolicyFactory {
  createPolicy(name: string, rules: { createScriptURL(url: string): string }): ScriptUrlPolicy;
}

// The policy turns this single URL into a script URL, and refuses any other.
function workerUrlOnly(url: string): string {
  if (url !== WORKER_SCRIPT_URL) {
    throw new TypeError(`Not the site's worker: ${url}`);
  }
  return url;
}

/**
 * The worker's script URL as the registration takes it. Where the browser has Trusted Types,
 * a trusted script URL of the site's own policy, so that registering is no string sink;
 * elsewhere, the plain URL. Typed as a string: the DOM typings know no Trusted Types.
 */
export function workerScriptSource(view: Window): string {
  const factory = (view as Window & { trustedTypes?: ScriptUrlPolicyFactory }).trustedTypes;
  if (factory === undefined) {
    return WORKER_SCRIPT_URL;
  }
  const policy = factory.createPolicy(POLICY_NAME, { createScriptURL: workerUrlOnly });
  return policy.createScriptURL(WORKER_SCRIPT_URL) as string;
}
