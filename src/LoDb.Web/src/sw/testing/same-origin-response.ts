/**
 * A response as a same-origin fetch gives it (`type: 'basic'`). A Response built in a test
 * has the type `default`, which the worker never keeps.
 */
export function sameOriginResponse(body: string, init: ResponseInit = {}): Response {
  const response = new Response(body, init);
  Object.defineProperty(response, 'type', { value: 'basic' });
  return response;
}
