import { isPlatformBrowser } from '@angular/common';
import { DOCUMENT, DestroyRef, PLATFORM_ID, inject } from '@angular/core';
import { VideoPlayback } from './video-playback';

/**
 * The playback of the component that calls it, tied to its life: the video pauses when the
 * tab hides, and stops for good when the component goes, as when the reader leaves the page.
 */
export function injectVideoPlayback(): VideoPlayback {
  const playback = new VideoPlayback();
  const document = inject(DOCUMENT);
  const destroyRef = inject(DestroyRef);
  if (!isPlatformBrowser(inject(PLATFORM_ID))) {
    return playback;
  }
  const onVisibilityChange = () => playback.onVisibilityChange(document.hidden);
  document.addEventListener('visibilitychange', onVisibilityChange);
  destroyRef.onDestroy(() => {
    document.removeEventListener('visibilitychange', onVisibilityChange);
    playback.dispose();
  });
  return playback;
}
