/**
 * How many times a champion has earned its per-level gain at `level` (1 to 18), on the
 * game's curve: none at level 1, 17 at level 18, the late levels weighing more than the
 * early ones. A stat grows by `g × (n − 1) × (0.7025 + 0.0175 × (n − 1))`.
 */
export function growthFactor(level: number): number {
  const steps = level - 1;
  return steps * (0.7025 + 0.0175 * steps);
}
