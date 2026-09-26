/**
 * How a stat grows with the level: by a flat gain, by a percentage of its base (the attack
 * speed), or not at all (move speed, attack range).
 */
export type StatKind = 'flat' | 'percent' | 'static';
