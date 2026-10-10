// Deterministic walks: the same index always gives the same request, so a run is repeatable
// and two runs of a bench never ask the same thing by accident.
// Plain module: imported by the k6 scripts and by node --test alike.

// A walk steps by about the golden section of its size, the first such step coprime with
// the size: `index * step mod size` is then a permutation of the indexes, and consecutive
// positions land far apart, so every dimension changes from one tuple to the next.
const GOLDEN_SECTION = (Math.sqrt(5) - 1) / 2;
const LETTERS = 'abcdefghijklmnopqrstuvwxyz';
const TERM_LENGTH = 6;
// xorshift32, seeded per index: enough to look typed, nothing cryptographic.
const XORSHIFT = [13, 17, 5];
const SEED = 0x9e3779b9;
const UINT32 = 0x100000000;

function greatestCommonDivisor(a, b) {
  return b === 0 ? a : greatestCommonDivisor(b, a % b);
}

// The product of a position and the step must stay an exact integer.
function stepFor(size) {
  if (size * size > Number.MAX_SAFE_INTEGER) {
    throw new RangeError(`A walk of ${size} is too large to scramble exactly.`);
  }
  let step = Math.max(1, Math.round(size * GOLDEN_SECTION));
  while (greatestCommonDivisor(step, size) !== 1) {
    step += 1;
  }
  return step;
}

/**
 * The `index`-th tuple of a walk over every combination of `sizes`, one digit per dimension.
 * As many tuples as there are combinations are all distinct, in a scrambled order: a crawl
 * spreads over versions, languages and pages, it does not count through them.
 */
export function tupleAt(sizes, index) {
  const size = sizes.reduce((product, length) => product * length, 1);
  if (size === 0) {
    throw new RangeError('A walk needs every dimension to hold something.');
  }
  let position = (index % size) * stepFor(size) % size;
  return sizes.map((length) => {
    const digit = position % length;
    position = Math.floor(position / length);
    return digit;
  });
}

function nextState(state) {
  let next = state;
  next ^= next << XORSHIFT[0];
  next ^= next >>> XORSHIFT[1];
  next ^= next << XORSHIFT[2];
  return next >>> 0;
}

/** A search a visitor could type, six letters, different from one index to the next. */
export function searchTermAt(index) {
  let state = (SEED ^ (index % UINT32)) >>> 0 || SEED;
  let term = '';
  for (let letter = 0; letter < TERM_LENGTH; letter += 1) {
    state = nextState(state);
    term += LETTERS[state % LETTERS.length];
  }
  return term;
}
