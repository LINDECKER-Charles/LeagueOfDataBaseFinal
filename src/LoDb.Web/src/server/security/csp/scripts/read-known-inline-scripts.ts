import { readFileSync } from 'node:fs';
import { KnownInlineScripts } from './known-inline-scripts';

/**
 * The inline scripts of the build's index templates, read once at start: the server render's
 * (`index.server.html`) and the browser's (`index.csr.html`, which also carries the deferred
 * stylesheets' loader that critical CSS adds to every render). A missing template stops the
 * server: without it, every page would refuse its own scripts.
 */
export function readKnownInlineScripts(templatePaths: readonly string[]): KnownInlineScripts {
  return new KnownInlineScripts(templatePaths.map((path) => readFileSync(path, 'utf8')));
}
