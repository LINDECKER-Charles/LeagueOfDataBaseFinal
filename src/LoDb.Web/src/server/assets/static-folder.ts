import express, { type RequestHandler } from 'express';
import { relative } from 'node:path';
import { documentPolicyHeaders } from '../security/csp/document-policy-headers';
import { staticCacheControl } from '../static-cache-control';

/**
 * Serves the files of `root` as they are, with the Cache-Control of their name
 * (`staticCacheControl`); a missing file falls through to the next handler. No index and
 * no redirect: a folder is never a page.
 */
export function staticFolder(root: string): RequestHandler {
  return express.static(root, {
    index: false,
    redirect: false,
    cacheControl: false,
    setHeaders: (response, filePath) => {
      response.setHeader('Cache-Control', staticCacheControl(relative(root, filePath)));
      // The offline page: a document of its own, with no script at all.
      if (filePath.endsWith('.html')) {
        for (const [name, value] of Object.entries(documentPolicyHeaders([]))) {
          response.setHeader(name, value);
        }
      }
    },
  });
}
