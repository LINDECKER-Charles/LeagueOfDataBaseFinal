function valueAt(tree, path) {
  return path.split('.').reduce((node, key) => (node === undefined ? undefined : node[key]), tree);
}

function setAt(tree, path, value) {
  const keys = path.split('.');
  const last = keys.pop();
  let node = tree;
  for (const key of keys) {
    node[key] ??= {};
    node = node[key];
  }
  node[last] = value;
}

/**
 * The sub-tree of `tree` holding only the dotted `paths`. Throws on a path the tree lacks: a
 * label that would otherwise show its raw key.
 */
export function pickKeys(tree, paths) {
  const picked = {};
  for (const path of paths) {
    const value = valueAt(tree, path);
    if (value === undefined) {
      throw new Error(`no message at ${path}`);
    }
    setAt(picked, path, value);
  }
  return picked;
}
