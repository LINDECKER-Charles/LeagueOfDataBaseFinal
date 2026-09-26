/**
 * Progress of an application update (Velopack on desktop, live update on Android). `ready`
 * means an update waits for `applyUpdate()`; the web never leaves `none`.
 */
export type UpdateState = 'none' | 'downloading' | 'ready';
