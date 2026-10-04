// Structural comparison of reference files, used by --check to prove that a replay
// reproduces the committed references once normalised.

const isObject = (value) => value !== null && typeof value === 'object';

/**
 * Lists every path where `actual` differs from `expected` (key order ignored).
 * @returns {string[]}
 */
export function differences(expected, actual, where = '') {
  if (!isObject(expected) || !isObject(actual)) {
    return Object.is(expected, actual)
      ? []
      : [`${where || '/'}: expected ${JSON.stringify(expected)}, got ${JSON.stringify(actual)}`];
  }
  if (Array.isArray(expected) !== Array.isArray(actual)) {
    return [`${where || '/'}: expected ${Array.isArray(expected) ? 'an array' : 'an object'}`];
  }
  const names = new Set([...Object.keys(expected), ...Object.keys(actual)]);
  return [...names].flatMap((name) => differences(expected[name], actual[name],
    `${where}/${exchangeLabel(expected, name)}`));
}

// Inside the exchange list, label entries by step id rather than by index.
function exchangeLabel(container, name) {
  const entry = Array.isArray(container) ? container[name] : undefined;
  return entry?.id ? `${name}(${entry.id})` : name;
}
