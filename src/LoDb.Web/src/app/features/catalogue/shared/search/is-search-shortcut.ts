import { SEARCH_SHORTCUT_KEY } from './search-shortcut-key';

const EDITABLE_TAGS = new Set(['INPUT', 'TEXTAREA', 'SELECT']);

function isTyping(element: Element | null): boolean {
  if (element === null) {
    return false;
  }
  const editable = (element as Partial<HTMLElement>).isContentEditable === true;
  return EDITABLE_TAGS.has(element.tagName) || editable;
}

/**
 * Whether a key press asks for the search field: the bare `/`, left alone while the reader
 * types elsewhere or holds a modifier.
 */
export function isSearchShortcut(event: KeyboardEvent, focused: Element | null): boolean {
  const hasModifier = event.ctrlKey || event.metaKey || event.altKey;
  return event.key === SEARCH_SHORTCUT_KEY && !hasModifier && !isTyping(focused);
}
