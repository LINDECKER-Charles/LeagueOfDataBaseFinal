/**
 * Builds an ESLint visitor that hands every module specifier of a file to `check`: static
 * imports, re-exports and dynamic `import()`. `no-restricted-imports` ignores the last two,
 * and a lazy `import()` is precisely how a feature would pull a native plugin or a
 * sibling feature without anyone noticing.
 *
 * @param {(node: import('estree').Node, source: string) => void} check
 * @returns {import('eslint').Rule.RuleListener}
 */
export function importSources(check) {
  const visit = (node) => {
    const source = node.source;
    if (source?.type === 'Literal' && typeof source.value === 'string') {
      check(source, source.value);
    }
  };

  return {
    ImportDeclaration: visit,
    ExportNamedDeclaration: visit,
    ExportAllDeclaration: visit,
    ImportExpression: visit,
  };
}
