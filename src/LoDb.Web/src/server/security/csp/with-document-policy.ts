import { documentPolicyHeaders } from './document-policy-headers';
import type { ScriptPolicy } from './scripts/script-policy';

const HTML = /^text\/html\b/i;

/**
 * The response of a render, with the policy headers of its document: its inline scripts are
 * admitted by `policy.known`, and any other one is left out and reported. A page is rendered
 * whole before it is sent, so reading its body costs a copy, not a delay. Anything other than
 * HTML goes through untouched.
 */
export async function withDocumentPolicy(
  rendered: Response,
  policy: ScriptPolicy,
): Promise<Response> {
  if (!HTML.test(rendered.headers.get('Content-Type') ?? '') || rendered.body === null) {
    return rendered;
  }
  const html = await rendered.text();
  const { hashes, unexpected } = policy.known.of(html);
  if (unexpected > 0) {
    policy.onUnexpected(unexpected);
  }
  const headers = new Headers(rendered.headers);
  for (const [name, value] of Object.entries(documentPolicyHeaders(hashes))) {
    headers.set(name, value);
  }
  return new Response(html, {
    status: rendered.status,
    statusText: rendered.statusText,
    headers,
  });
}
