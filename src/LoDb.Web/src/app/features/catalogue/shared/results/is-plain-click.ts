const MAIN_BUTTON = 0;

/**
 * Whether a click on a link is a plain one the page may handle itself; a modified or middle
 * click opens the link elsewhere, as the reader asked.
 */
export function isPlainClick(event: MouseEvent): boolean {
  const hasModifier = event.ctrlKey || event.metaKey || event.shiftKey || event.altKey;
  return event.button === MAIN_BUTTON && !hasModifier;
}
