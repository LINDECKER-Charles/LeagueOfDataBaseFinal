import type { Data } from '@angular/router';
import { PRELOAD_KEY } from './preload-key';

/**
 * The data of a route whose code is read ahead once the page is idle: the catalogue's mounts
 * and their list and detail pages, where a reader most often goes next.
 */
export const PRELOADED_ROUTE: Data = { [PRELOAD_KEY]: true };
