/** What is ticked in a list of older patches, and what goes first. */
export interface PatchSelection {
  /** The versions ticked. */
  readonly selected: ReadonlySet<string>;
  /** The version a Shift range starts from: the last one toggled alone. */
  readonly anchor: string | null;
  /** The versions rendered first, in this order; always ticked, 100 at most. */
  readonly priority: readonly string[];
  /**
   * The Shift range being drawn: the ticks before its first extension from the anchor, and
   * the row it reaches. Each extension starts again from `base`, so rows a shrinking range
   * leaves get their state back; any other action ends it (null).
   */
  readonly extent: { readonly base: ReadonlySet<string>; readonly to: string } | null;
}

/** What the operator does to a selection; the component only sends these. */
export type SelectionAction =
  | { readonly kind: 'toggle'; readonly version: string }
  | { readonly kind: 'extend'; readonly version: string; readonly rows: readonly string[] }
  | { readonly kind: 'season'; readonly versions: readonly string[] }
  | { readonly kind: 'set'; readonly versions: readonly string[]; readonly on: boolean }
  | { readonly kind: 'replace'; readonly versions: readonly string[] }
  | { readonly kind: 'invert'; readonly rows: readonly string[] }
  | { readonly kind: 'clear' }
  | { readonly kind: 'priority'; readonly version: string }
  | { readonly kind: 'move'; readonly version: string; readonly by: -1 | 1 }
  | { readonly kind: 'paste'; readonly versions: readonly string[]; readonly append: boolean };

type ActionOf<K extends SelectionAction['kind']> = Extract<SelectionAction, { kind: K }>;
type Effects = {
  readonly [K in SelectionAction['kind']]: (
    selection: PatchSelection,
    action: ActionOf<K>,
  ) => PatchSelection;
};

// PassRequestReader.MaxPriorityVersions: the API refuses more.
const MAX_PRIORITY = 100;

function flipped(selected: ReadonlySet<string>, versions: readonly string[]): Set<string> {
  const next = new Set(selected);
  for (const version of versions) {
    if (!next.delete(version)) {
      next.add(version);
    }
  }
  return next;
}

function marked(selected: ReadonlySet<string>, versions: readonly string[], on: boolean) {
  const next = new Set(selected);
  for (const version of versions) {
    if (on) {
      next.add(version);
    } else {
      next.delete(version);
    }
  }
  return next;
}

function unique(versions: readonly string[]): string[] {
  return [...new Set(versions)];
}

function toggle(selection: PatchSelection, version: string): PatchSelection {
  return { ...selection, selected: flipped(selection.selected, [version]), anchor: version };
}

// Every row between the anchor and `version` takes the anchor's state, from the ticks the
// range started from; without an anchor among the rows, a Shift click is a click.
function extend(selection: PatchSelection, action: ActionOf<'extend'>): PatchSelection {
  const { anchor } = selection;
  const from = anchor === null ? -1 : action.rows.indexOf(anchor);
  const to = action.rows.indexOf(action.version);
  if (anchor === null || from < 0 || to < 0) {
    return { ...toggle(selection, action.version), extent: null };
  }
  const base = selection.extent?.base ?? selection.selected;
  const range = action.rows.slice(Math.min(from, to), Math.max(from, to) + 1);
  return {
    ...selection,
    selected: marked(base, range, base.has(anchor)),
    extent: { base, to: action.version },
  };
}

function season(selection: PatchSelection, versions: readonly string[]): PatchSelection {
  const isWhole = versions.every((version) => selection.selected.has(version));
  return { ...selection, selected: marked(selection.selected, versions, !isWhole) };
}

function prioritise(selection: PatchSelection, version: string): PatchSelection {
  const { priority } = selection;
  if (priority.includes(version)) {
    return { ...selection, priority: priority.filter((first) => first !== version) };
  }
  if (priority.length >= MAX_PRIORITY) {
    return selection;
  }
  return {
    ...selection,
    selected: marked(selection.selected, [version], true),
    priority: [...priority, version],
  };
}

function move(selection: PatchSelection, action: ActionOf<'move'>): PatchSelection {
  const priority = [...selection.priority];
  const index = priority.indexOf(action.version);
  const other = index + action.by;
  if (index < 0 || other < 0 || other >= priority.length) {
    return selection;
  }
  [priority[index], priority[other]] = [priority[other], priority[index]];
  return { ...selection, priority };
}

function paste(selection: PatchSelection, action: ActionOf<'paste'>): PatchSelection {
  const first = action.append ? [...selection.priority, ...action.versions] : action.versions;
  return {
    ...selection,
    selected: marked(selection.selected, action.versions, true),
    priority: unique(first).slice(0, MAX_PRIORITY),
  };
}

const EFFECTS: Effects = {
  toggle: (selection, action) => toggle(selection, action.version),
  extend,
  season: (selection, action) => season(selection, action.versions),
  set: (selection, action) => ({
    ...selection,
    selected: marked(selection.selected, action.versions, action.on),
  }),
  replace: (selection, action) => ({
    ...selection,
    selected: new Set(action.versions),
    anchor: null,
  }),
  invert: (selection, action) => ({
    ...selection,
    selected: flipped(selection.selected, action.rows),
  }),
  clear: () => ({ selected: new Set(), anchor: null, priority: [], extent: null }),
  priority: (selection, action) => prioritise(selection, action.version),
  move,
  paste,
};

function effectOf<K extends SelectionAction['kind']>(
  selection: PatchSelection,
  action: ActionOf<K>,
): PatchSelection {
  return (EFFECTS[action.kind] as Effects[K])(selection, action);
}

/**
 * The selection once `action` applied (§ 3.3.2), the only source of truth of the list. Two
 * rules follow every action: a version no longer ticked leaves the priorities (the API would
 * drop it silently, and the summary would announce a wrong order), and any action but an
 * extension ends the range being drawn.
 */
export function selectionAfter(selection: PatchSelection, action: SelectionAction): PatchSelection {
  const next = effectOf(selection, action);
  return {
    ...next,
    priority: next.priority.filter((version) => next.selected.has(version)),
    extent: action.kind === 'extend' ? next.extent : null,
  };
}
