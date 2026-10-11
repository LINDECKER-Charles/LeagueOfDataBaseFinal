import { compileMessage } from './compile-message.mjs';

function compileEntry(value, path, errors) {
  if (typeof value === 'string') {
    try {
      return compileMessage(value);
    } catch (error) {
      errors.push(`${path}: ${error.message.split('\n')[0]}`);
      return value;
    }
  }
  if (value !== null && typeof value === 'object' && !Array.isArray(value)) {
    return compileTree(value, path, errors);
  }
  errors.push(`${path}: a message must be a string, not ${JSON.stringify(value)}`);
  return value;
}

function compileTree(tree, prefix, errors) {
  const compiled = {};
  for (const [key, value] of Object.entries(tree)) {
    compiled[key] = compileEntry(value, prefix ? `${prefix}.${key}` : key, errors);
  }
  return compiled;
}

/**
 * Compiles every message of a catalogue (`compileMessage`), keeping its tree. Collects the
 * messages that do not compile, by dotted key, rather than stopping at the first one, so that
 * a build lists them all at once; `errors` is empty when the catalogue compiled.
 */
export function compileCatalogue(catalogue) {
  const errors = [];
  const compiled = compileTree(catalogue, '', errors);
  return { compiled, errors };
}
