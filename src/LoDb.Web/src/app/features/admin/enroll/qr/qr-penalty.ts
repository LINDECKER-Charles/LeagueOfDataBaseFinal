import type { QrModules } from './qr-matrix';

// The weights of ISO/IEC 18004 §7.8.3: runs of five or more (N1), 2 × 2 blocks (N2),
// finder look-alikes (N3) and imbalance between dark and light (N4).
const N1 = 3;
const N2 = 3;
const N3 = 40;
const N4 = 10;
const LONG_RUN = 5;
// A finder look-alike is dark-light-dark-light-dark in 1:1:3:1:1, with 4 light around.
const FINDER_RATIOS: readonly number[] = [1, 1, 3, 1, 1];
const FINDER_QUIET = 4;
const HISTORY = 7;
// The imbalance counts every 5 % away from half dark.
const BALANCE_STEPS = 20;
const HALF_STEPS = 10;

// Whether five runs are in 1:1:3:1:1, whatever the size of a module.
function finderShaped(runs: readonly number[]): boolean {
  const module = runs[0] ?? 0;
  return module > 0 && FINDER_RATIOS.every((ratio, index) => runs[index] === module * ratio);
}

// The look-alikes the last seven runs end, the light quiet zone on either side: the runs
// are newest first, the light after the pattern, its five runs, then the light before it.
function finderLikes(history: readonly number[]): number {
  if (!finderShaped(history.slice(1, 1 + FINDER_RATIOS.length))) {
    return 0;
  }
  const module = history[1] ?? 0;
  const after = history[0] ?? 0;
  const before = history[HISTORY - 1] ?? 0;
  const lightAfter = after >= module * FINDER_QUIET && before >= module ? 1 : 0;
  const lightBefore = before >= module * FINDER_QUIET && after >= module ? 1 : 0;
  return lightAfter + lightBefore;
}

// The runs and look-alikes of one row or column; the quiet zone is light on both ends.
function linePenalty(line: readonly boolean[]): number {
  const history = new Array<number>(HISTORY).fill(0);
  const push = (run: number) => {
    history.unshift(history[0] === 0 ? run + line.length : run);
    history.pop();
  };
  let score = 0;
  let color = false;
  let run = 0;
  for (const dark of line) {
    if (dark === color) {
      run++;
      score += run === LONG_RUN ? N1 : run > LONG_RUN ? 1 : 0;
    } else {
      push(run);
      score += color ? 0 : finderLikes(history) * N3;
      color = dark;
      run = 1;
    }
  }
  if (color) {
    push(run);
    run = 0;
  }
  push(run + line.length);
  return score + finderLikes(history) * N3;
}

function blocksPenalty(modules: QrModules): number {
  let score = 0;
  modules.slice(1).forEach((row, y) => {
    const above = modules[y] ?? [];
    for (let x = 1; x < row.length; x++) {
      const color = row[x];
      if (color === row[x - 1] && color === above[x] && color === above[x - 1]) {
        score += N2;
      }
    }
  });
  return score;
}

function balancePenalty(modules: QrModules): number {
  const total = modules.length * modules.length;
  const dark = modules.flat().filter(Boolean).length;
  const steps = Math.ceil(Math.abs(dark * BALANCE_STEPS - total * HALF_STEPS) / total) - 1;
  return steps * N4;
}

/**
 * How badly a masked symbol reads (ISO/IEC 18004 §7.8.3): the encoder keeps the mask with
 * the lowest penalty.
 */
export function qrPenalty(modules: QrModules): number {
  const columns = modules.map((_, x) => modules.map((row) => row[x] ?? false));
  const lines = [...modules, ...columns].reduce((sum, line) => sum + linePenalty(line), 0);
  return lines + blocksPenalty(modules) + balancePenalty(modules);
}
