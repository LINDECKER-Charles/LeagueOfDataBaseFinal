function isTree(value) {
  return value !== null && typeof value === 'object' && !Array.isArray(value);
}

// Transloco fell back on a key whose value is falsy (missing, empty): the completion does too.
function isMessage(value) {
  return typeof value === 'string' && value !== '';
}

function completedEntry(referenceValue, ownValue) {
  if (isTree(referenceValue)) {
    return completeCatalogue(referenceValue, isTree(ownValue) ? ownValue : {});
  }
  return isMessage(ownValue) ? ownValue : referenceValue;
}

/**
 * A locale's catalogue completed with the reference (`en`), key by key: what Transloco's
 * per-key fallback rendered, computed once at build time instead of loading both catalogues
 * on every page. The reference's keys come first, in its order; keys only the locale has are
 * kept, as Transloco would still have found them.
 */
export function completeCatalogue(reference, own) {
  const complete = {};
  for (const [key, value] of Object.entries(reference)) {
    complete[key] = completedEntry(value, own[key]);
  }
  for (const [key, value] of Object.entries(own)) {
    if (!Object.hasOwn(complete, key)) {
      complete[key] = value;
    }
  }
  return complete;
}
