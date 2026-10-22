import type { ChangeSubject } from '../../../../../../core/api/generated/models/change-subject';
import { CHANGE_FIELD } from '../../../../../../core/api/generated/models/change-field-array';
import { ROOT_CATALOGUES } from '../../../../../../core/testing/root-catalogues';
import { translateFrom } from '../../../../../../core/testing/translate-from';
import { fieldLabelOf } from './field-label-of';
import { fieldLabelsOf } from './field-labels-of';
import { subjectNamesOf } from './subject-names-of';

const EN = translateFrom(ROOT_CATALOGUES.en);
const FR = translateFrom(ROOT_CATALOGUES.fr);

function subject(kind: ChangeSubject['kind'], key: string, name: string | null = null) {
  return { kind, key, name };
}

describe('fieldLabelOf', () => {
  // One label per field template, in the root catalogue every head and block reads.
  it.each(Object.entries(ROOT_CATALOGUES))('labels every field template in %s', (_locale, root) => {
    const labels = (root as { detail: { field: Record<string, string> } }).detail.field;

    expect(CHANGE_FIELD.filter((field) => !labels[field])).toEqual([]);
  });

  it('labels a field without a subject by its template alone', () => {
    expect(fieldLabelOf({ field: 'goldTotal', subject: null }, EN)).toBe('Total cost');
    expect(fieldLabelOf({ field: 'goldTotal', subject: null }, FR)).toBe('Prix total');
  });

  it.each([
    ['stat', subject('stat', 'attack_damage'), 'Attack damage'],
    ['statPerLevel', subject('stat', 'mana_regen'), 'Resource regen per level'],
    ['statPercent', subject('stat', 'move_speed'), 'Move speed (%)'],
    ['rating', subject('rating', 'difficulty'), 'Profile: Difficulty'],
    ['runeSlot', subject('runeSlot', 'keystone'), 'Runes: Keystone'],
    ['runeSlot', subject('runeSlot', 'row2'), 'Runes: Slot 2'],
    [
      'spellCooldown',
      subject('spellSlot', 'q', 'Orb of Deception'),
      'Orb of Deception (Q): cooldown',
    ],
    ['spellCost', subject('spellSlot', 'w'), 'W: cost'],
    ['runeLongDesc', subject('rune', '8005', 'Press the Attack'), 'Press the Attack: description'],
    ['skinName', subject('skin', '103001'), 'Skin name (103001)'],
  ] as const)('weaves the subject of %s in: %s', (field, theSubject, expected) => {
    expect(fieldLabelOf({ field, subject: theSubject }, EN)).toBe(expected);
  });

  it('keeps a stat or a rating no table knows as its key', () => {
    expect(fieldLabelOf({ field: 'stat', subject: subject('stat', 'lethality') }, EN)).toBe(
      'lethality',
    );
    expect(fieldLabelOf({ field: 'rating', subject: subject('rating', 'luck') }, EN)).toBe(
      'Profile: luck',
    );
  });

  // The history reads the index alone: the page's own entity names the subjects.
  it('names the subjects the API leaves unnamed from the page entity', () => {
    const names = subjectNamesOf({
      ...{ canonicalPath: 'champions/Ahri', canonical: { version: null, path: 'champions/Ahri' } },
      ...{ state: null, history: [], next: null, presentInLatest: true, changes: null },
      abilities: [{ slot: 'q', name: 'Orb of Deception' }],
      skins: [{ id: '103001', name: 'Dynasty Ahri' }],
      slots: [{ runes: [{ id: 8005, name: 'Press the Attack' }] }],
    });

    expect(
      fieldLabelOf({ field: 'spellName', subject: subject('spellSlot', 'q') }, EN, names),
    ).toBe('Orb of Deception (Q): name');
    expect(fieldLabelOf({ field: 'skinName', subject: subject('skin', '103001') }, EN, names)).toBe(
      'Skin name (Dynasty Ahri)',
    );
    expect(fieldLabelOf({ field: 'runeName', subject: subject('rune', '8005') }, EN, names)).toBe(
      'Press the Attack: name',
    );
  });
});

describe('fieldLabelsOf', () => {
  it('names each label once, in the order of the fields', () => {
    const fields = [
      { field: 'roles', subject: null },
      { field: 'name', subject: null },
      { field: 'roles', subject: null },
    ] as const;

    expect(fieldLabelsOf(fields, EN)).toEqual(['Roles', 'Name']);
  });
});
