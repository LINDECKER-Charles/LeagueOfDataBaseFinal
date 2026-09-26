import type { RuneTreeOption } from '../../../../core/api/generated/models/rune-tree-option';
import { RuneDraft } from './rune-draft';
import { RUNE_LIMITS } from './rune-limits';
import { secondarySlotIndex } from './secondary-slot-index';

const PRECISION = 8000;
const DOMINATION = 8100;
const SORCERY = 8200;
const { ghostSlot, keystoneSlot } = RUNE_LIMITS;

function fullDraft(): RuneDraft {
  return RuneDraft.empty()
    .withPrimaryStyle(PRECISION)
    .withPrimaryPerk(0, 8005)
    .withPrimaryPerk(1, 9101)
    .withPrimaryPerk(2, 9104)
    .withPrimaryPerk(3, 8014)
    .withSecondaryStyle(DOMINATION)
    .withSecondaryPerk(1, 8126)
    .withSecondaryPerk(2, 8138);
}

function dominationSide(): RuneDraft {
  return RuneDraft.empty().withPrimaryStyle(PRECISION).withSecondaryStyle(DOMINATION);
}

// A tree whose rows hold the given rune ids, the keystones first.
function tree(id: number, rows: number[][]): RuneTreeOption {
  const image = { status: 'absent' as const };
  const slots = rows.map((row) =>
    row.map((runeId) => ({
      id: runeId,
      key: `${runeId}`,
      name: `${runeId}`,
      image,
      shortDesc: '',
    })),
  );
  return { id, key: `${id}`, name: `${id}`, image, slots };
}

describe('RuneDraft', () => {
  describe('primary side', () => {
    it('starts empty', () => {
      const draft = RuneDraft.empty();

      expect(draft.primaryStyleId).toBeNull();
      expect(draft.primaryPerks).toEqual([null, null, null, null]);
      expect(draft.secondaryPicks).toEqual([]);
    });

    it('holds one rune per slot', () => {
      const draft = RuneDraft.empty()
        .withPrimaryStyle(PRECISION)
        .withPrimaryPerk(1, 9101)
        .withPrimaryPerk(1, 9111);

      expect(draft.primaryPerks).toEqual([null, 9111, null, null]);
    });

    it('ignores slots out of range', () => {
      const draft = RuneDraft.empty().withPrimaryStyle(PRECISION);

      expect(draft.withPrimaryPerk(4, 1)).toBe(draft);
      expect(draft.withPrimaryPerk(-1, 1)).toBe(draft);
    });

    it('clears its runes on another primary tree', () => {
      const next = fullDraft().withPrimaryStyle(SORCERY);

      expect(next.primaryPerks).toEqual([null, null, null, null]);
      expect(next.secondaryStyleId).toBe(DOMINATION);
    });

    it('keeps everything when the same tree is picked again', () => {
      const draft = fullDraft();

      expect(draft.withPrimaryStyle(PRECISION)).toBe(draft);
    });

    it('clears the secondary side when its tree becomes the primary one', () => {
      const next = fullDraft().withPrimaryStyle(DOMINATION);

      expect(next.secondaryStyleId).toBeNull();
      expect(next.secondaryPicks).toEqual([]);
    });
  });

  describe('secondary side, the game client eviction rule', () => {
    it('refuses the primary tree as the secondary one', () => {
      const draft = RuneDraft.empty().withPrimaryStyle(PRECISION);

      expect(draft.withSecondaryStyle(PRECISION)).toBe(draft);
    });

    it('clears its picks on another secondary tree', () => {
      const next = fullDraft().withSecondaryStyle(SORCERY);

      expect(next.secondaryStyleId).toBe(SORCERY);
      expect(next.secondaryPicks).toEqual([]);
    });

    it('never takes a keystone row pick', () => {
      const draft = fullDraft();

      expect(draft.withSecondaryPerk(keystoneSlot, 8112)).toBe(draft);
    });

    it('replaces the pick of a row already used', () => {
      const draft = dominationSide().withSecondaryPerk(1, 8126).withSecondaryPerk(1, 8139);

      expect(draft.secondaryPicks).toEqual([{ slotIndex: 1, perkId: 8139 }]);
    });

    it('evicts the oldest pick for a third row', () => {
      const draft = dominationSide()
        .withSecondaryPerk(1, 8126)
        .withSecondaryPerk(2, 8138)
        .withSecondaryPerk(3, 8106);

      expect(draft.secondaryPicks).toEqual([
        { slotIndex: 2, perkId: 8138 },
        { slotIndex: 3, perkId: 8106 },
      ]);
    });

    it('makes a replaced pick the newest before evicting', () => {
      const draft = dominationSide()
        .withSecondaryPerk(1, 8126)
        .withSecondaryPerk(2, 8138)
        .withSecondaryPerk(1, 8139)
        .withSecondaryPerk(3, 8106);

      expect(draft.secondaryPicks).toEqual([
        { slotIndex: 1, perkId: 8139 },
        { slotIndex: 3, perkId: 8106 },
      ]);
    });

    it('tells the rows in use and when both picks are made', () => {
      const one = dominationSide().withSecondaryPerk(2, 8138);
      const both = one.withSecondaryPerk(3, 8106);

      expect(one.isSecondarySlotUsed(2)).toBe(true);
      expect(one.isSecondarySlotUsed(1)).toBe(false);
      expect(one.isSecondaryPick(8138)).toBe(true);
      expect(one.isSecondaryFull()).toBe(false);
      expect(both.isSecondaryFull()).toBe(true);
    });
  });

  describe('the page handed to the API', () => {
    it('tells whether the page is complete', () => {
      expect(RuneDraft.empty().isComplete()).toBe(false);
      expect(fullDraft().isComplete()).toBe(true);
    });

    it('hands over a complete page', () => {
      expect(fullDraft().toRunePage()).toEqual({
        primaryStyleId: PRECISION,
        primarySelections: [8005, 9101, 9104, 8014],
        secondaryStyleId: DOMINATION,
        secondarySelections: [8126, 8138],
      });
    });

    it('hands over a partial page without inventing anything', () => {
      const draft = RuneDraft.empty().withPrimaryStyle(PRECISION).withPrimaryPerk(2, 9104);

      expect(draft.toRunePage()).toEqual({
        primaryStyleId: PRECISION,
        primarySelections: [9104],
        secondaryStyleId: 0,
        secondarySelections: [],
      });
    });
  });

  describe('a stored page (edit and import)', () => {
    const stored = {
      primaryStyleId: PRECISION,
      primarySelections: [8005, 9101, 9104, 8014],
      secondaryStyleId: DOMINATION,
      secondarySelections: [8126, 8138],
    };

    it('anchors the secondary picks through the row resolver', () => {
      const draft = RuneDraft.of(stored, (_styleId, perkId) => {
        const rows: Record<number, number> = { 8126: 1, 8138: 3 };
        return rows[perkId] ?? null;
      });

      expect(draft.primaryPerks).toEqual([8005, 9101, 9104, 8014]);
      expect(draft.secondaryPicks).toEqual([
        { slotIndex: 1, perkId: 8126 },
        { slotIndex: 3, perkId: 8138 },
      ]);
      expect(draft.secondaryStyleId).toBe(DOMINATION);
    });

    it('keeps the runes it cannot place in sight, on the ghost row', () => {
      const draft = RuneDraft.of(stored, () => null);

      expect(draft.secondaryPicks).toEqual([
        { slotIndex: ghostSlot, perkId: 8126 },
        { slotIndex: ghostSlot, perkId: 8138 },
      ]);
      expect(draft.secondaryGhosts()).toHaveLength(2);
    });

    it('reads zeroed trees as not chosen', () => {
      const blank = {
        primaryStyleId: 0,
        primarySelections: [],
        secondaryStyleId: 0,
        secondarySelections: [],
      };
      const draft = RuneDraft.of(blank, () => null);

      expect(draft.primaryStyleId).toBeNull();
      expect(draft.secondaryStyleId).toBeNull();
      expect(draft.primaryPerks).toEqual([null, null, null, null]);
    });

    it('re-anchors its ghost rows once the trees are loaded, true ghosts staying', () => {
      const trees = [tree(DOMINATION, [[8112], [8126], [8138], [8106]])];
      const draft = RuneDraft.of({ ...stored, secondarySelections: [8126, 9999] }, () => null);

      expect(draft.reanchored(trees).secondaryPicks).toEqual([
        { slotIndex: 1, perkId: 8126 },
        { slotIndex: ghostSlot, perkId: 9999 },
      ]);
      expect(fullDraft().reanchored(trees)).toEqual(fullDraft());
    });
  });

  describe('secondarySlotIndex', () => {
    const trees = [tree(DOMINATION, [[8112], [8126, 8139], [8138]])];

    it('finds the minor row of a rune', () => {
      expect(secondarySlotIndex(trees, DOMINATION, 8139)).toBe(1);
      expect(secondarySlotIndex(trees, DOMINATION, 8138)).toBe(2);
    });

    it('knows no keystone, no unknown rune and no unknown tree', () => {
      expect(secondarySlotIndex(trees, DOMINATION, 8112)).toBeNull();
      expect(secondarySlotIndex(trees, DOMINATION, 1)).toBeNull();
      expect(secondarySlotIndex(trees, SORCERY, 8126)).toBeNull();
    });
  });
});
