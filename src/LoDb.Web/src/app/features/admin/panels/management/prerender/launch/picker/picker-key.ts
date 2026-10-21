import type { SelectionAction } from '../../common/selection/patch-selection';
import type { PatchLine, PickerLine, PickerRows, SeasonLine } from './picker-rows';

/** What a key pressed on a line of the list does; the component applies each part. */
export interface PickerCommand {
  /** The key of the line to move the focus to. */
  readonly focus?: string;
  /** What to send to the selection. */
  readonly action?: SelectionAction;
  /** The season (`16`) to fold or unfold, and whether it ends open. */
  readonly fold?: { readonly season: string; readonly isOpen: boolean };
  /** Go back to the last grouped selection. */
  readonly undo?: true;
  /** Move the focus to the text filter. */
  readonly filter?: true;
  /** A character typed: the search moves to the first version starting with the buffer. */
  readonly search?: string;
}

/** The key, as a KeyboardEvent reports it. */
export interface PickerKey {
  readonly key: string;
  readonly shiftKey: boolean;
  readonly ctrlKey: boolean;
  readonly metaKey: boolean;
}

type Handler = (key: PickerKey, line: PickerLine, rows: PickerRows) => PickerCommand | null;

const PAGE = 10;
const SEASON_PREFIX = 'season:';
const SEARCHED = /^[\d.]$/;

function seasonOf(line: SeasonLine): string {
  return line.key.slice(SEASON_PREFIX.length);
}

// The line `by` lines away from `line`, kept within the list.
function lineAt(rows: PickerRows, line: PickerLine, by: number): PickerLine {
  const at = rows.lines.findIndex((other) => other.key === line.key);
  const to = Math.min(rows.lines.length - 1, Math.max(0, at + by));
  return rows.lines[to] ?? line;
}

// ↑ ↓: the next line; with Shift on a patch, the range reaches it too.
function step(by: number): Handler {
  return (key, line, rows) => {
    const next = lineAt(rows, line, by);
    const reaches = key.shiftKey && line.kind === 'patch' && next.kind === 'patch';
    const action: SelectionAction | undefined = reaches
      ? { kind: 'extend', version: next.key, rows: rows.order }
      : undefined;
    return { focus: next.key, action };
  };
}

function space(key: PickerKey, line: PickerLine, rows: PickerRows): PickerCommand | null {
  if (line.kind === 'season') {
    return key.shiftKey ? null : { action: { kind: 'season', versions: line.versions } };
  }
  return key.shiftKey
    ? { action: { kind: 'extend', version: line.key, rows: rows.order } }
    : { action: { kind: 'toggle', version: line.key } };
}

function right(_: PickerKey, line: PickerLine, rows: PickerRows): PickerCommand | null {
  if (line.kind === 'patch') {
    return null;
  }
  if (!line.isOpen) {
    return { fold: { season: seasonOf(line), isOpen: true } };
  }
  const first = lineAt(rows, line, 1);
  return first.kind === 'patch' ? { focus: first.key } : null;
}

function left(_: PickerKey, line: PickerLine): PickerCommand | null {
  if (line.kind === 'patch') {
    return { focus: line.seasonKey };
  }
  return line.isOpen ? { fold: { season: seasonOf(line), isOpen: false } } : null;
}

const toggleOf = (line: PatchLine): PickerCommand => ({
  action: { kind: 'toggle', version: line.key },
});

const HANDLERS: Readonly<Record<string, Handler>> = {
  ArrowDown: step(1),
  ArrowUp: step(-1),
  PageDown: (_, line, rows) => ({ focus: lineAt(rows, line, PAGE).key }),
  PageUp: (_, line, rows) => ({ focus: lineAt(rows, line, -PAGE).key }),
  Home: (_, line, rows) => ({ focus: rows.lines[0]?.key ?? line.key }),
  End: (_, line, rows) => ({ focus: rows.lines.at(-1)?.key ?? line.key }),
  ' ': space,
  Enter: (_, line) =>
    line.kind === 'patch'
      ? toggleOf(line)
      : { fold: { season: seasonOf(line), isOpen: !line.isOpen } },
  ArrowRight: right,
  ArrowLeft: left,
  p: (_, line) =>
    line.kind === 'patch' ? { action: { kind: 'priority', version: line.key } } : null,
  '/': () => ({ filter: true }),
};

// Ctrl or ⌘ with A or Z: every visible patch, or undo.
function shortcut(key: PickerKey, rows: PickerRows): PickerCommand | null {
  const letter = key.key.toLowerCase();
  if (letter === 'a') {
    return { action: { kind: 'set', versions: rows.order, on: true } };
  }
  return letter === 'z' ? { undo: true } : null;
}

/**
 * What `key` does on `line` of the list (§ 3.3.5): a pure translation the component applies.
 * Null leaves the key to the browser and the dialog: Tab, and Escape, which closes it.
 */
export function pickerActionOf(
  key: PickerKey,
  line: PickerLine,
  rows: PickerRows,
): PickerCommand | null {
  if (key.ctrlKey || key.metaKey) {
    return shortcut(key, rows);
  }
  if (SEARCHED.test(key.key)) {
    return { search: key.key };
  }
  const handler = HANDLERS[key.key.length === 1 ? key.key.toLowerCase() : key.key];
  return handler ? handler(key, line, rows) : null;
}
