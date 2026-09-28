import { documentPolicy } from './document-policy';
import { inlineScriptHashes } from './inline-script-hashes';

const HTML = /^text\/html\b/i;

/**
 * The response of a render, with the Content-Security-Policy of its document. A page is
 * rendered whole before it is sent, so reading its body costs a copy, not a delay. Anything
 * other than HTML goes through untouched.
 */
export async function withDocumentPolicy(rendered: Response): Promise<Response> {
  if (!HTML.test(rendered.headers.get('Content-Type') ?? '') || rendered.body === null) {
    return rendered;
  }
  const html = await rendered.text();
  const headers = new Headers(rendered.headers);
  headers.set('Content-Security-Policy', documentPolicy(inlineScriptHashes(html)));
  return new Response(html, {
    status: rendered.status,
    statusText: rendered.statusText,
    headers,
  });
}
