import { HttpErrorResponse } from '@angular/common/http';
import { isChunkLoadFailure } from './is-chunk-load-failure';

const CHUNK = 'https://league-of-data-base.com/build/chunk-ABCD1234.js';

describe('isChunkLoadFailure', () => {
  it.each([
    ['Chromium', new TypeError(`Failed to fetch dynamically imported module: ${CHUNK}`)],
    ['Firefox', new TypeError(`error loading dynamically imported module: ${CHUNK}`)],
    ['WebKit', new TypeError('Importing a module script failed.')],
  ])('recognises the failure %s reports', (_engine, error) => {
    expect(isChunkLoadFailure(error)).toBe(true);
  });

  it.each([
    ['a bug', new TypeError('undefined is not a function')],
    ['an API error', new HttpErrorResponse({ status: 500 })],
    ['a bare message', 'Failed to fetch dynamically imported module'],
    ['nothing', undefined],
  ])('leaves %s to the error page', (_case, error) => {
    expect(isChunkLoadFailure(error)).toBe(false);
  });
});
