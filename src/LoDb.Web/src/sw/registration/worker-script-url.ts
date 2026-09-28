/**
 * Where the worker is served. Fixed and at the root: the root scope needs no extra header, and
 * the previous site's worker lived at this very URL, so its registrations update to this one.
 */
export const WORKER_SCRIPT_URL = '/sw.js';
