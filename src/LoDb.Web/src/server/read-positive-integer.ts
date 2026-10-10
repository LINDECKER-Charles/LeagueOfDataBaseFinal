type Environment = Readonly<Record<string, string | undefined>>;

/**
 * The positive integer `variable` holds, or `fallback` when it is absent or empty. A
 * misconfigured variable stops the server at startup rather than serving on a guess.
 */
export function readPositiveInteger(env: Environment, variable: string, fallback: number): number {
  const value = env[variable];
  if (value === undefined || value === '') {
    return fallback;
  }
  const parsed = Number(value);
  if (!Number.isInteger(parsed) || parsed < 1) {
    throw new Error(`${variable} must be a positive integer, got "${value}".`);
  }
  return parsed;
}
