/**
 * A cooldown as Data Dragon's `cooldownBurn` spells it: one value when every rank shares it,
 * else each rank's, joined by slashes; a dash when there is none.
 */
export function cooldownTextOf(cooldown: readonly number[]): string {
  const ranks = new Set(cooldown);
  if (ranks.size === 0) {
    return '–';
  }
  return ranks.size === 1 ? String(cooldown[0]) : cooldown.join('/');
}
