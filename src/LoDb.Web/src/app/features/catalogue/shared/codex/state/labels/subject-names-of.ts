import type { ChangeSubject } from '../../../../../../core/api/generated/models/change-subject';
import type { StatefulDetails } from '../stateful-details';
import type { SubjectNames } from './subject-names';

// One name per `{kind}:{key}`, as the subjects of the fields designate their sub-entity.
function keyOf(kind: ChangeSubject['kind'], key: string): string {
  return `${kind}:${key}`;
}

/**
 * The names the page's own entity gives the subjects of its fields: its spells by slot, its
 * skins and runes by id. The history reads the index alone, its subjects unnamed: the page
 * names them, in its language and version.
 */
export function subjectNamesOf(details: StatefulDetails): SubjectNames {
  const names = new Map<string, string>([
    ...(details.abilities ?? []).map(
      (ability) => [keyOf('spellSlot', ability.slot), ability.name] as const,
    ),
    ...(details.skins ?? []).map((skin) => [keyOf('skin', skin.id), skin.name] as const),
    ...(details.slots ?? []).flatMap((row) =>
      row.runes.map((rune) => [keyOf('rune', String(rune.id)), rune.name] as const),
    ),
  ]);
  return (subject) => names.get(keyOf(subject.kind, subject.key)) ?? null;
}
