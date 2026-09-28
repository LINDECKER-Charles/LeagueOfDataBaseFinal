import type { ImageState } from './image-state';

/**
 * State of an image element from what the DOM reports. A complete image with no intrinsic
 * width is a failed one (or had an empty source): the browser gives no other error signal
 * for an image that failed before a listener was attached.
 */
export function imageStateOf(complete: boolean, naturalWidth: number): ImageState {
  if (!complete) {
    return 'loading';
  }
  return naturalWidth > 0 ? 'loaded' : 'error';
}
