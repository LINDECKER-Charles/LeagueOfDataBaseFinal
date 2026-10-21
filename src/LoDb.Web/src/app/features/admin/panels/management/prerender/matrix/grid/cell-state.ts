/**
 * The state of a (version, locale) in a pass or in an epoch, each an `admin.prerender.
 * common.cells.*` key and a style of the matrix (§ 4.4).
 */
export type CellState =
  | 'pending'
  | 'running'
  | 'stopped'
  | 'complete'
  | 'complete_with_failures'
  | 'failed'
  | 'interrupted'
  | 'partial'
  | 'absent'
  | 'out_of_scope';

/** Whose cells: a pass's progress, or an epoch's coverage. */
export type CellMode = 'pass' | 'coverage';

// The codes of the API (Http/Views/UnitCells.cs), the two modes sharing c, w and f.
const SHARED: Readonly<Record<string, CellState>> = {
  c: 'complete',
  w: 'complete_with_failures',
  f: 'failed',
};
const BY_MODE: Readonly<Record<CellMode, Readonly<Record<string, CellState>>>> = {
  pass: {
    ...SHARED,
    '.': 'pending',
    r: 'running',
    z: 'stopped',
    i: 'interrupted',
    _: 'out_of_scope',
  },
  coverage: { ...SHARED, p: 'partial', '-': 'absent' },
};

/** The state of a cell code in `mode`; a code the admin does not know is out of scope. */
export function cellStateOf(code: string, mode: CellMode): CellState {
  return BY_MODE[mode][code] ?? 'out_of_scope';
}
