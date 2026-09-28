import { PLATFORM_ID } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { injectVideoPlayback } from './inject-video-playback';
import type { VideoPlayback } from './video-playback';

// A fake <video>: the playback only reads and writes this surface.
function fakeVideo(overrides: Partial<HTMLVideoElement> = {}): HTMLVideoElement {
  return {
    paused: false,
    duration: 10,
    currentTime: 0,
    muted: false,
    play: vi.fn(),
    pause: vi.fn(),
    ...overrides,
  } as unknown as HTMLVideoElement;
}

function set<K extends keyof HTMLVideoElement>(
  video: HTMLVideoElement,
  key: K,
  value: HTMLVideoElement[K],
): void {
  Object.assign(video, { [key]: value });
}

let frames = new Map<number, FrameRequestCallback>();
let lastFrame = 0;

function flushFrames(): void {
  const callbacks = [...frames.values()];
  frames = new Map();
  callbacks.forEach((callback) => callback(0));
}

function tabHides(hidden: boolean): void {
  vi.spyOn(document, 'hidden', 'get').mockReturnValue(hidden);
  document.dispatchEvent(new Event('visibilitychange'));
}

// The playback of a component, torn down as the component would be.
function playbackOf(video: HTMLVideoElement | null = null): VideoPlayback {
  const playback = TestBed.runInInjectionContext(injectVideoPlayback);
  playback.adopt(video);
  return playback;
}

describe('VideoPlayback', () => {
  beforeEach(() => {
    frames = new Map();
    vi.stubGlobal('requestAnimationFrame', (callback: FrameRequestCallback) => {
      frames.set(++lastFrame, callback);
      return lastFrame;
    });
    vi.stubGlobal(
      'cancelAnimationFrame',
      vi.fn((id: number) => frames.delete(id)),
    );
  });

  afterEach(() => {
    TestBed.resetTestingModule();
    vi.unstubAllGlobals();
    vi.restoreAllMocks();
  });

  it('plays a paused video and pauses a playing one', () => {
    const video = fakeVideo({ paused: true });
    const playback = playbackOf(video);

    playback.toggle();
    expect(video.play).toHaveBeenCalledOnce();

    set(video, 'paused', false);
    playback.toggle();
    expect(video.pause).toHaveBeenCalledOnce();
  });

  it('tracks the progress on animation frames while playing, and stops on pause', () => {
    const video = fakeVideo({ currentTime: 2.5 });
    const playback = playbackOf(video);

    playback.onPlay();
    expect(playback.isPaused()).toBe(false);
    flushFrames();
    expect(playback.progress()).toBe(0.25);

    set(video, 'currentTime', 5);
    flushFrames();
    expect(playback.progress()).toBe(0.5);

    set(video, 'paused', true);
    playback.onPause();
    expect(playback.isPaused()).toBe(true);
    expect(frames.size).toBe(0);
  });

  it('samples the final position once on pause', () => {
    const playback = playbackOf(fakeVideo({ paused: true, currentTime: 7.5 }));

    playback.onPause();
    expect(playback.progress()).toBe(0.75);
    expect(frames.size).toBe(0);
  });

  it('keeps the last progress while the duration is unknown', () => {
    const playback = playbackOf(fakeVideo({ duration: NaN, currentTime: 3 }));

    playback.onPlay();
    flushFrames();
    expect(playback.progress()).toBe(0);
  });

  it('pauses the previous element and resets the state when another clip comes', () => {
    const previous = fakeVideo({ paused: true, currentTime: 5 });
    const playback = playbackOf(previous);
    playback.onPause();
    expect(playback.progress()).toBe(0.5);

    playback.adopt(fakeVideo());
    expect(previous.pause).toHaveBeenCalledOnce();
    expect(playback.progress()).toBe(0);
    expect(playback.isPaused()).toBe(false);
  });

  it('starts muted, and applies the sticky mute choice to every new element', () => {
    const first = fakeVideo({ muted: false });
    const playback = playbackOf(first);
    expect(playback.isMuted()).toBe(true);
    expect(first.muted).toBe(true);

    playback.toggleMute();
    expect(playback.isMuted()).toBe(false);
    expect(first.muted).toBe(false);

    const next = fakeVideo({ muted: true });
    playback.adopt(next);
    expect(next.muted).toBe(false);
  });

  it('cancels the frame loop when the component goes', () => {
    const playback = playbackOf(fakeVideo());
    playback.onPlay();
    flushFrames();

    TestBed.resetTestingModule();
    expect(cancelAnimationFrame).toHaveBeenCalled();
    expect(frames.size).toBe(0);
  });

  it('pauses the current video when the component goes, so its sound stops', () => {
    const video = fakeVideo();
    playbackOf(video);

    TestBed.resetTestingModule();
    expect(video.pause).toHaveBeenCalled();
  });

  it('pauses a playing video when the tab hides, and resumes it on return', () => {
    const video = fakeVideo({ paused: false });
    playbackOf(video);

    tabHides(true);
    expect(video.pause).toHaveBeenCalledOnce();

    set(video, 'paused', true);
    tabHides(false);
    expect(video.play).toHaveBeenCalledOnce();
  });

  it('does not resume a video the reader paused before the tab hid', () => {
    const video = fakeVideo({ paused: true });
    playbackOf(video);

    tabHides(true);
    expect(video.pause).not.toHaveBeenCalled();

    tabHides(false);
    expect(video.play).not.toHaveBeenCalled();
  });

  it('listens to nothing on the server', () => {
    TestBed.configureTestingModule({ providers: [{ provide: PLATFORM_ID, useValue: 'server' }] });
    const video = fakeVideo({ paused: false });
    playbackOf(video);

    tabHides(true);
    TestBed.resetTestingModule();
    expect(video.pause).not.toHaveBeenCalled();
  });
});
