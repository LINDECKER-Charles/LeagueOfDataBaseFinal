import type { ChangeSubject } from '../../../../../../core/api/generated/models/change-subject';
import type { GameStat } from '../../../../../../core/api/generated/models/game-stat';
import type { SubjectKind } from '../../../../../../core/api/generated/models/subject-kind';
import { GAME_STAT } from '../../../../../../core/api/generated/models/game-stat-array';
import type { Translate } from '../../texts/translate';
import type { SubjectNames } from './subject-names';

/** Riot's four ratings of a champion, each labelled on its profile. */
const RATINGS: readonly string[] = ['attack', 'defense', 'magic', 'difficulty'];
const KEYSTONE = 'keystone';
const ROW = /^row(\d+)$/;
// A champion's own resource (mana, energy, fury…) regenerates: its board says so, not "mana".
const RESOURCE_REGEN: GameStat = 'mana_regen';

type SubjectLabel = (key: string, translate: Translate, name: string | null) => string;

function statLabel(key: string, translate: Translate): string {
  if (key === RESOURCE_REGEN) {
    return translate('champion.detail.stats.resource_regen');
  }
  return GAME_STAT.includes(key as GameStat) ? translate(`stat.${key}`) : key;
}

function runeRowLabel(key: string, translate: Translate): string {
  if (key === KEYSTONE) {
    return translate('rune.detail.keystone');
  }
  const row = ROW.exec(key);
  return row === null ? key : translate('rune.detail.slot_n', { n: row[1] });
}

function ratingLabel(key: string, translate: Translate): string {
  return RATINGS.includes(key) ? translate(`champion.detail.profile.${key}`) : key;
}

// A spell is known by its key; its name, when known, follows: "Orb of Deception (Q)".
function spellLabel(key: string, name: string | null): string {
  const slot = key.toUpperCase();
  return name === null ? slot : `${name} (${slot})`;
}

const LABELS: Readonly<Record<SubjectKind, SubjectLabel>> = {
  stat: statLabel,
  rating: ratingLabel,
  runeSlot: runeRowLabel,
  spellSlot: (key, _translate, name) => spellLabel(key, name),
  rune: (key, _translate, name) => name ?? key,
  skin: (key, _translate, name) => name ?? key,
};

/**
 * What a changed field's subject reads as: a stat or a rating by its label, a row of runes by
 * its place, a spell by its key and name, a rune or a skin by its name, which the API gives
 * where it read a catalogue and the page's entity otherwise; the bare key as a last resort.
 */
export function subjectLabelOf(
  subject: ChangeSubject,
  translate: Translate,
  names?: SubjectNames,
): string {
  const name = subject.name ?? names?.(subject) ?? null;
  return LABELS[subject.kind](subject.key, translate, name);
}
