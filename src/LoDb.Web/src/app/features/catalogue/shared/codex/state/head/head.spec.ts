import type { ChangedField } from '../../../../../../core/api/generated/models/changed-field';
import type { EntityState } from '../../../../../../core/api/generated/models/entity-state';
import type { PageContext } from '../../../../../../core/context/page-context';
import type { CatalogueEntry } from '../../../../../../core/routing/catalogue/catalogue-entry';
import { detailStateOf } from '../../../../../../core/testing/detail-state-of';
import { ROOT_CATALOGUES } from '../../../../../../core/testing/root-catalogues';
import { SEO_CATALOGUES } from '../../../../../../core/testing/seo-catalogues';
import { translateFrom } from '../../../../../../core/testing/translate-from';
import type { CatalogueTexts } from '../../head/catalogue-texts';
import type { StatefulDetails } from '../stateful-details';
import { stateHeadOf } from './state-head-of';

const TEXTS: CatalogueTexts = {
  seo: translateFrom(SEO_CATALOGUES.en),
  main: translateFrom(ROOT_CATALOGUES.en),
};
const PATH = 'items/3078-trinity-force';
const DESCRIPTION = 'Trinity Force in League of Legends.';
const PINNED: PageContext = { locale: 'en', version: '11.5.1', pinned: true, language: 'en_US' };

function field(field: ChangedField['field']): ChangedField {
  return { field, subject: null };
}

// The state of the page: opened at 11.5.1, closed at `last` (open when null).
function stateOf(last: string | null, fields: Partial<EntityState> = {}): EntityState {
  const opener = { version: '11.5.1', path: PATH };
  return {
    openerVersion: '11.5.1',
    lastVersion: last,
    isOpen: last === null,
    isOpener: true,
    kind: 'changed',
    changedFields: [field('goldTotal')],
    openerLink: opener,
    canonical: last === null ? { version: null, path: PATH } : opener,
    ...fields,
  };
}

function entryOf(state: EntityState | null, context = PINNED): CatalogueEntry<StatefulDetails> {
  return { context, details: { ...detailStateOf(PATH), canonicalPath: PATH, state } };
}

describe('stateHeadOf', () => {
  it('names the run of patches of a closed state on its opener, its changes first', () => {
    const head = stateHeadOf(entryOf(stateOf('11.9.1')), TEXTS, DESCRIPTION);

    expect(head).toEqual({
      description: `Changed in patch 11.5.1: Total cost. ${DESCRIPTION}`,
      titlePatches: { first: '11.5.1', last: '11.9.1' },
    });
  });

  it('names the one patch of a state of one version, once', () => {
    const head = stateHeadOf(entryOf(stateOf('11.5.1')), TEXTS, DESCRIPTION);

    expect(head.titlePatches).toEqual({ first: '11.5.1', last: '11.5.1' });
  });

  it('names three fields at most, then an ellipsis', () => {
    const fields = ['goldTotal', 'goldBase', 'recipe', 'tags'].map((name) =>
      field(name as ChangedField['field']),
    );

    const entry = entryOf(stateOf('11.9.1', { changedFields: fields }));

    expect(stateHeadOf(entry, TEXTS, DESCRIPTION).description).toBe(
      `Changed in patch 11.5.1: Total cost, Purchase cost, Recipe… ${DESCRIPTION}`,
    );
  });

  it.each([
    ['new', 'New in patch 11.5.1.'],
    ['returned', 'Back in patch 11.5.1.'],
  ] as const)('says that an entity is %s rather than its fields', (kind, summary) => {
    const head = stateHeadOf(entryOf(stateOf('11.9.1', { kind })), TEXTS, DESCRIPTION);

    expect(head.description).toBe(`${summary} ${DESCRIPTION}`);
  });

  it('names the run of patches of a state opened at the start of the index, no change told', () => {
    const head = stateHeadOf(entryOf(stateOf('11.9.1', { kind: 'baseline' })), TEXTS, DESCRIPTION);

    expect(head).toEqual({
      description: DESCRIPTION,
      titlePatches: { first: '11.5.1', last: '11.9.1' },
    });
  });

  it.each([
    ['the open state', entryOf(stateOf(null))],
    ['a state it did not open', entryOf(stateOf('11.9.1', { isOpener: false }))],
    ['a page outside the index', entryOf(null)],
    ['the latest version', entryOf(stateOf('11.9.1'), { ...PINNED, pinned: false })],
  ])('keeps the title and the description of %s', (_page, entry) => {
    expect(stateHeadOf(entry, TEXTS, DESCRIPTION)).toEqual({ description: DESCRIPTION });
  });
});
