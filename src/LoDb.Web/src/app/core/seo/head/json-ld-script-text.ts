// The site's own policy for the text of its structured data, which the pages' Trusted Types
// admit (src/server/security/csp/trusted-types-policy.ts).
const POLICY_NAME = 'lodb#json-ld';

interface ScriptPolicy {
  createScript(text: string): unknown;
}

interface ScriptPolicyFactory {
  createPolicy(name: string, rules: { createScript(text: string): string }): ScriptPolicy;
}

// The policy turns JSON into script text, and refuses anything else: a JSON-LD block holds data.
function jsonOnly(text: string): string {
  JSON.parse(text);
  return text;
}

/**
 * How the text of a `<script type="application/ld+json">` reaches the DOM. Assigning a
 * script's text is a Trusted Types sink, whatever the script's type: where the browser has
 * Trusted Types, the text goes through the site's own policy, which admits JSON only;
 * elsewhere, and on the server, it stays a string. Typed as a string: the DOM typings know no
 * Trusted Types. Builds the policy once: call it once per document.
 */
export function jsonLdScriptText(view: Window | null): (json: string) => string {
  const factory = (view as (Window & { trustedTypes?: ScriptPolicyFactory }) | null)?.trustedTypes;
  if (factory === undefined) {
    return (json) => json;
  }
  const policy = factory.createPolicy(POLICY_NAME, { createScript: jsonOnly });
  return (json) => policy.createScript(json) as string;
}
