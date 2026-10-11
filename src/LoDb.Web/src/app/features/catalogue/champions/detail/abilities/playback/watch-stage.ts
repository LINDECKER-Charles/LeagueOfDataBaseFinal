// Half of the stage on screen: the clip starts there, and pauses below it.
const ON_SCREEN_RATIO = 0.5;

/**
 * Calls `onChange` with whether at least half of the stage is on screen, at once and each
 * time it crosses that line; returns the way to stop. Without IntersectionObserver the stage
 * counts as on screen, as the clip played before the observer existed.
 */
export function watchStage(stage: Element, onChange: (isOnScreen: boolean) => void): () => void {
  if (typeof IntersectionObserver === 'undefined') {
    onChange(true);
    return () => undefined;
  }
  const observer = new IntersectionObserver(
    (entries) => {
      const entry = entries.at(-1);
      if (entry !== undefined) {
        onChange(entry.isIntersecting && entry.intersectionRatio >= ON_SCREEN_RATIO);
      }
    },
    { threshold: ON_SCREEN_RATIO },
  );
  observer.observe(stage);
  return () => observer.disconnect();
}
