import { signal } from '@angular/core';

/**
 * Play/pause, mute and progress of a looping, chrome-less `<video>`. The progress is sampled
 * on animation frames while the video plays only (`timeupdate` is too coarse for a smooth
 * bar), so an idle page schedules nothing.
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
  private resumeOnVisible = false;

  adopt(video: HTMLVideoElement | null): void {
    if (video === this.video) {
      return;
    }
    this.video?.pause();
    this.cancelFrame();
    this.video = video;
    this.isPaused.set(false);
    this.progress.set(0);
    if (video !== null) {
      video.muted = this.isMuted();
    }
  }

  toggle(): void {
    const video = this.video;
    if (video === null) {
      return;
    }
    if (video.paused) {
      void video.play();
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

  /**
   * A hidden tab must not keep playing: pause on hide, and resume on return only what was
   * playing, never overriding the reader's own pause.
   */
  onVisibilityChange(hidden: boolean): void {
    const video = this.video;
    if (video === null) {
      return;
    }
    if (hidden) {
      this.resumeOnVisible = !video.paused;
      if (this.resumeOnVisible) {
        video.pause();
      }
    } else if (this.resumeOnVisible) {
      this.resumeOnVisible = false;
      void video.play();
    }
  }

  /** Leaving the page: a detached `<video>` can play on until collected. */
  dispose(): void {
    this.cancelFrame();
    this.video?.pause();
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
