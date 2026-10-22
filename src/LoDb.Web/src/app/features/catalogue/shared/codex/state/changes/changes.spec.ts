import type { FieldChange } from '../../../../../../core/api/generated/models/field-change';
import { ROOT_CATALOGUES } from '../../../../../../core/testing/root-catalogues';
import { translateFrom } from '../../../../../../core/testing/translate-from';
import { changeRowsOf } from './change-rows-of';

const EN = { locale: 'en', translate: translateFrom(ROOT_CATALOGUES.en) } as const;
const FR = { locale: 'fr', translate: translateFrom(ROOT_CATALOGUES.fr) } as const;

function change(fields: Partial<FieldChange>): FieldChange {
  return {
    field: 'goldTotal',
    subject: null,
    kind: 'value',
    valueKind: 'number',
    before: [],
    after: [],
    segments: [],
    ...fields,
  };
}

function values(...texts: string[]): FieldChange['after'] {
  return texts.map((value) => ({ value, label: null }));
}

describe('changeRowsOf', () => {
  it('keeps a text diff as its plain-text runs, in order', () => {
    const segments = [
      { kind: 'same', text: 'Deals ' },
      { kind: 'removed', text: '<b>40</b>' },
      { kind: 'added', text: '60' },
      { kind: 'same', text: ' damage.\nHeals.' },
    ] as const;

    const [row] = changeRowsOf(
      [change({ field: 'description', kind: 'text', segments: [...segments] })],
      EN,
    );

    expect(row).toEqual({ label: 'Description', isText: true, pieces: segments });
  });

  it('shows a value struck out, an arrow, then the value after, in the locale', () => {
    const [row] = changeRowsOf([change({ before: values('3000'), after: values('3100.5') })], FR);

    expect(row?.label).toBe('Prix total');
    expect(row?.pieces).toEqual([
      { kind: 'removed', text: '3 000' },
      { kind: 'arrow', text: '→' },
      { kind: 'added', text: '3 100,5' },
    ]);
  });

  it('shows the value after alone when the language holds the same, or the field is new', () => {
    const [row] = changeRowsOf([change({ after: values('3100') })], EN);

    expect(row?.pieces).toEqual([{ kind: 'same', text: '3,100' }]);
  });

  it('strikes out the value of a field that is gone', () => {
    const [row] = changeRowsOf([change({ before: values('true'), valueKind: 'flag' })], EN);

    expect(row?.pieces).toEqual([{ kind: 'removed', text: 'Yes' }]);
  });

  it('writes a percentage, a range of the whole map and an entity by its name', () => {
    const rows = changeRowsOf(
      [
        change({ valueKind: 'percent', before: values('0.07'), after: values('0.1') }),
        change({
          field: 'range',
          kind: 'list',
          valueKind: 'range',
          after: values('25000', 'global'),
        }),
        change({
          field: 'requiredChampion',
          valueKind: 'entity',
          after: [{ value: 'Gangplank', label: 'Gangplank' }],
        }),
      ],
      EN,
    );

    expect(rows.map((row) => row.pieces.map((piece) => piece.text))).toEqual([
      ['7%', '→', '10%'],
      ['25,000, Global'],
      ['Gangplank'],
    ]);
  });

  it('names codes through the front tables, and an unknown one as it came', () => {
    const rows = changeRowsOf(
      [
        change({
          field: 'roles',
          kind: 'list',
          valueKind: 'code',
          before: values('Mage'),
          after: values('Mage', 'Support'),
        }),
        change({
          field: 'tags',
          kind: 'set',
          valueKind: 'code',
          before: values('Boots'),
          after: values('CriticalStrike'),
        }),
        change({
          field: 'maps',
          kind: 'set',
          valueKind: 'code',
          before: values('11', '99'),
          after: values('11'),
        }),
        change({ field: 'modes', kind: 'set', valueKind: 'code', after: values('URF') }),
      ],
      FR,
    );

    expect(rows.map((row) => row.pieces)).toEqual([
      [
        { kind: 'removed', text: 'Mage' },
        { kind: 'arrow', text: '→' },
        { kind: 'added', text: 'Mage, Support' },
      ],
      [
        { kind: 'removed', text: 'Boots' },
        { kind: 'added', text: 'Critical Strike' },
      ],
      [{ kind: 'removed', text: '99' }],
      [{ kind: 'added', text: 'URF' }],
    ]);
  });

  it('shows a set equal in this language as its values after', () => {
    const [row] = changeRowsOf(
      [
        change({
          field: 'maps',
          kind: 'set',
          valueKind: 'code',
          before: values('11'),
          after: values('11'),
        }),
      ],
      FR,
    );

    expect(row?.pieces).toEqual([{ kind: 'same', text: "Faille de l'invocateur" }]);
  });
});
