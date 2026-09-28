import type { RunePage } from '../../../../core/api/generated/models/rune-page';
import type { RuneTreeOption } from '../../../../core/api/generated/models/rune-tree-option';
import { RUNE_LIMITS } from './rune-limits';
import type { SecondaryPick } from './secondary-pick';
import { secondarySlotIndex } from './secondary-slot-index';

interface RuneDraftState {
  readonly primaryStyleId: number | null;
  /** One rune per primary slot, the keystone first; null for a slot not picked yet. */
  readonly primaryPerks: readonly (number | null)[];
  readonly secondaryStyleId: number | null;
  /** The oldest pick first, at most two. */
  readonly secondaryPicks: readonly SecondaryPick[];
}

/** The minor row of a stored secondary rune, null when the trees at hand do not hold it. */
type SlotResolver = (styleId: number, perkId: number) => number | null;

function blankPerks(): (number | null)[] {
  return Array.from({ length: RUNE_LIMITS.primarySlots }, () => null);
}

/**
 * A rune page being edited, with the game client's rules: one rune per primary slot, and two
 * secondary runes from two different minor rows. Picking in a row already used replaces its
 * rune; picking in a third row evicts the oldest pick. Every change answers a new draft, and
 * the same one when it changes nothing.
 */
export class RuneDraft implements RuneDraftState {
  readonly primaryStyleId: number | null;
  readonly primaryPerks: readonly (number | null)[];
  readonly secondaryStyleId: number | null;
  readonly secondaryPicks: readonly SecondaryPick[];

  private constructor(state: RuneDraftState) {
    this.primaryStyleId = state.primaryStyleId;
    this.primaryPerks = state.primaryPerks;
    this.secondaryStyleId = state.secondaryStyleId;
    this.secondaryPicks = state.secondaryPicks;
  }

  static empty(): RuneDraft {
    const blank = { primaryStyleId: null, secondaryStyleId: null, secondaryPicks: [] };
    return new RuneDraft({ ...blank, primaryPerks: blankPerks() });
  }

  /**
   * The draft of a stored page. A secondary rune whose row `slotOf` cannot tell keeps the
   * ghost row: it stays in sight, for its author to replace, and never collides with a row.
   */
  static of(runes: RunePage, slotOf: SlotResolver): RuneDraft {
    const secondaryPicks = runes.secondarySelections
      .slice(0, RUNE_LIMITS.secondaryPicks)
      .map((perkId) => ({
        slotIndex: slotOf(runes.secondaryStyleId, perkId) ?? RUNE_LIMITS.ghostSlot,
        perkId,
      }));
    return new RuneDraft({
      primaryStyleId: runes.primaryStyleId || null,
      primaryPerks: blankPerks().map((_, slot) => runes.primarySelections[slot] ?? null),
      secondaryStyleId: runes.secondaryStyleId || null,
      secondaryPicks,
    });
  }

  /** Another primary tree clears its runes, and the secondary side when it was that tree. */
  withPrimaryStyle(styleId: number): RuneDraft {
    if (styleId === this.primaryStyleId) {
      return this;
    }
    const collides = styleId === this.secondaryStyleId;
    return this.with({
      primaryStyleId: styleId,
      primaryPerks: blankPerks(),
      secondaryStyleId: collides ? null : this.secondaryStyleId,
      secondaryPicks: collides ? [] : this.secondaryPicks,
    });
  }

  withPrimaryPerk(slotIndex: number, perkId: number): RuneDraft {
    if (slotIndex < 0 || slotIndex >= RUNE_LIMITS.primarySlots) {
      return this;
    }
    return this.with({
      primaryPerks: this.primaryPerks.map((perk, slot) => (slot === slotIndex ? perkId : perk)),
    });
  }

  /** The primary tree is never the secondary one; another tree clears the secondary picks. */
  withSecondaryStyle(styleId: number): RuneDraft {
    return styleId === this.primaryStyleId || styleId === this.secondaryStyleId
      ? this
      : this.with({ secondaryStyleId: styleId, secondaryPicks: [] });
  }

  /**
   * A secondary rune: never in the keystone row; it replaces the pick of its row, becoming
   * the newest, and a third row evicts the oldest pick.
   */
  withSecondaryPerk(slotIndex: number, perkId: number): RuneDraft {
    if (slotIndex === RUNE_LIMITS.keystoneSlot) {
      return this;
    }
    const kept = this.secondaryPicks.filter((pick) => pick.slotIndex !== slotIndex);
    const picks = [...kept, { slotIndex, perkId }];
    return this.with({ secondaryPicks: picks.slice(-RUNE_LIMITS.secondaryPicks) });
  }

  /** Re-anchors the ghost-row picks on trees just loaded; true ghosts stay ghosts. */
  reanchored(trees: readonly RuneTreeOption[]): RuneDraft {
    const styleId = this.secondaryStyleId;
    const ghost = RUNE_LIMITS.ghostSlot;
    if (styleId === null || this.secondaryPicks.every((pick) => pick.slotIndex !== ghost)) {
      return this;
    }
    const slotOf = (pick: SecondaryPick) =>
      pick.slotIndex === ghost
        ? (secondarySlotIndex(trees, styleId, pick.perkId) ?? ghost)
        : pick.slotIndex;
    return this.with({
      secondaryPicks: this.secondaryPicks.map((pick) => ({ ...pick, slotIndex: slotOf(pick) })),
    });
  }

  isComplete(): boolean {
    return (
      this.primaryStyleId !== null &&
      this.secondaryStyleId !== null &&
      this.primaryPerks.every((perk) => perk !== null) &&
      this.secondaryPicks.length === RUNE_LIMITS.secondaryPicks
    );
  }

  /** Whether both secondary picks are made: a row left free then reads as unavailable. */
  isSecondaryFull(): boolean {
    return this.secondaryPicks.length >= RUNE_LIMITS.secondaryPicks;
  }

  isSecondaryPick(perkId: number): boolean {
    return this.secondaryPicks.some((pick) => pick.perkId === perkId);
  }

  isSecondarySlotUsed(slotIndex: number): boolean {
    return this.secondaryPicks.some((pick) => pick.slotIndex === slotIndex);
  }

  /** The secondary runes no loaded tree holds. */
  secondaryGhosts(): readonly SecondaryPick[] {
    return this.secondaryPicks.filter((pick) => pick.slotIndex === RUNE_LIMITS.ghostSlot);
  }

  /**
   * The page as the API takes it, even incomplete: the missing picks are left out and a tree
   * not chosen is 0, so the API names what is missing with its own codes.
   */
  toRunePage(): RunePage {
    return {
      primaryStyleId: this.primaryStyleId ?? 0,
      primarySelections: this.primaryPerks.filter((perk): perk is number => perk !== null),
      secondaryStyleId: this.secondaryStyleId ?? 0,
      secondarySelections: this.secondaryPicks.map((pick) => pick.perkId),
    };
  }

  private with(patch: Partial<RuneDraftState>): RuneDraft {
    return new RuneDraft({ ...this, ...patch });
  }
}
