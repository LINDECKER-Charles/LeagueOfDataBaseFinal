import { signal } from '@angular/core';

/**
 * Play/pause, mute and progress of a looping, chrome-less `<video>`. The progress is sampled
 * on animation frames while the video plays only (`timeupdate` is too coarse for a smooth
 * bar), so an idle page schedules nothing.
 *
 * The clip plays while its stage is on screen and the tab shown, unless the reader paused it:
 * scrolled away or hidden, it pauses, and comes back playing. It starts only then, never on
 * render (`preload="none"`, no `autoplay`), so a clip below the fold costs nothing.
 *
 * Each clip comes in a fresh element: adopting it pauses the previous one, so its sound stops
 * at once, and applies the mute choice to the property, never the `muted` attribute alone,
 * which does not mute an element already created. The video starts muted, which keeps
 * autoplay within browser policy; the reader opts into sound, and the choice sticks from one
 * clip to the next.
 */
export class VideoPlayback {
  readonly isPaused = signal(false);
  readonly isMuted = signal(true);
  /** Position in the loop, from 0 to 1. */
  readonly progress = signal(0);

  private video: HTMLVideoElement | null = null;
  private frame = 0;
  private isOnScreen = false;
  private isTabHidden = false;
  private isHeldByReader = false;

  adopt(video: HTMLVideoElement | null): void {
    if (video === this.video) {
      return;
    }
    this.video?.pause();
    this.cancelFrame();
    this.video = video;
    this.isHeldByReader = false;
    this.isPaused.set(video !== null && !this.shouldPlay());
    this.progress.set(0);
    if (video !== null) {
      video.muted = this.isMuted();
      if (this.shouldPlay()) {
        play(video);
      }
    }
  }

  /** The reader's own play or pause, which scrolling and tab switches then respect. */
  toggle(): void {
    const video = this.video;
    if (video === null) {
      return;
    }
    this.isHeldByReader = !video.paused;
    if (video.paused) {
      play(video);
    } else {
      video.pause();
    }
  }

  toggleMute(): void {
    this.isMuted.update((muted) => !muted);
    if (this.video !== null) {
      this.video.muted = this.isMuted();
    }
  }

  /** The video's `play` event. */
  onPlay(): void {
    this.isPaused.set(false);
    this.cancelFrame();
    this.frame = requestAnimationFrame(() => this.sample());
  }

  /** The video's `pause` event: the loop stops on the final position. */
  onPause(): void {
    this.isPaused.set(true);
    this.cancelFrame();
    this.sample();
  }

  /** At least half of the stage came on screen, or left it. */
  onStageVisible(isOnScreen: boolean): void {
    this.isOnScreen = isOnScreen;
    this.sync();
  }

  /** A hidden tab must not keep playing. */
  onVisibilityChange(hidden: boolean): void {
    this.isTabHidden = hidden;
    this.sync();
  }

  /** Leaving the page: a detached `<video>` can play on until collected. */
  dispose(): void {
    this.cancelFrame();
    this.video?.pause();
  }

  private shouldPlay(): boolean {
    return this.isOnScreen && !this.isTabHidden && !this.isHeldByReader;
  }

  private sync(): void {
    const video = this.video;
    if (video === null) {
      return;
    }
    if (this.shouldPlay() && video.paused) {
      play(video);
    } else if (!this.shouldPlay() && !video.paused) {
      video.pause();
    }
  }

  private sample(): void {
    const video = this.video;
    if (video === null) {
      return;
    }
    if (video.duration > 0) {
      this.progress.set(video.currentTime / video.duration);
    }
    if (!video.paused) {
      this.frame = requestAnimationFrame(() => this.sample());
    }
  }

  private cancelFrame(): void {
    if (this.frame !== 0) {
      cancelAnimationFrame(this.frame);
      this.frame = 0;
    }
  }
}

// A play cut short by a pause (the stage scrolled away at once) or refused by the browser is
// no error: the element's own events keep the controls true to what it does.
function play(video: HTMLVideoElement): void {
  void video.play().catch(() => undefined);
}
