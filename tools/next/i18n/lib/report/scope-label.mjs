/** Name of a scope in the report: the root catalogue has no folder, hence no name. */
export function scopeLabel(scope) {
  return scope === '' ? 'racine' : scope;
}
