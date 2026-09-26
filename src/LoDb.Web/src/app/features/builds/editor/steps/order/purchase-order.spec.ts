import { PurchaseOrder } from './purchase-order';
import { STEP_LIMITS } from './step-limits';

const { maxItemsPerStep, maxSteps, maxTotalItems } = STEP_LIMITS;

function order(...items: string[][]): PurchaseOrder {
  return PurchaseOrder.of(items.map((ids, index) => ({ label: `Step ${index + 1}`, items: ids })));
}

function itemsOf(built: PurchaseOrder, step = 0): readonly string[] {
  return built.steps[step]?.items ?? [];
}

function firstItems(built: PurchaseOrder): (string | undefined)[] {
  return built.steps.map((step) => step.items[0]);
}

describe('PurchaseOrder', () => {
  describe('steps', () => {
    it('opens a stored build without steps on one blank step', () => {
      expect(PurchaseOrder.of([]).steps).toEqual([{ label: '', note: null, items: [] }]);
    });

    it('adds steps up to the cap, then refuses', () => {
      let built = PurchaseOrder.of([{ label: 'Start', items: [] }]);
      while (built.canAddStep()) {
        built = built.withStep();
      }

      expect(built.steps).toHaveLength(maxSteps);
      expect(built.withStep()).toBe(built);
    });

    it('removes a step by index, ignoring out-of-range ones and the last step', () => {
      const built = order(['1055'], ['3006']);

      expect(built.withoutStep(0).steps).toHaveLength(1);
      expect(built.withoutStep(5)).toBe(built);
      expect(built.withoutStep(-1)).toBe(built);
      expect(order(['1055']).withoutStep(0).steps).toHaveLength(1);
    });

    it('moves a step, clamped at the edges', () => {
      const built = order(['a'], ['b'], ['c']);

      expect(firstItems(built.movingStep(0, 1))).toEqual(['b', 'a', 'c']);
      expect(built.movingStep(0, -1)).toBe(built);
      expect(built.movingStep(2, 1)).toBe(built);
    });

    it('patches a label and a note without touching the original', () => {
      const built = order(['a']);
      const next = built.patchingStep(0, { label: 'Core', note: 'rush it' });

      expect(next.steps[0]).toEqual({ label: 'Core', note: 'rush it', items: ['a'] });
      expect(built.steps[0].label).toBe('Step 1');
      expect(built.patchingStep(9, { label: 'x' })).toBe(built);
    });
  });

  describe('item bounds', () => {
    it('caps the items of a step', () => {
      let built = order([]);
      for (let index = 0; index < maxItemsPerStep; index++) {
        built = built.withItem(0, '2003');
      }

      expect(itemsOf(built)).toHaveLength(maxItemsPerStep);
      expect(built.canAddItem(0)).toBe(false);
      expect(built.withItem(0, '2003')).toBe(built);
    });

    it('allows the same item twice (several purchases)', () => {
      const built = order([]).withItem(0, '2003').withItem(0, '2003');

      expect(itemsOf(built)).toEqual(['2003', '2003']);
    });

    it('caps the items of the whole build', () => {
      // 5 steps of 8 items: 40, the build's cap.
      let built = order([], [], [], [], [], []);
      for (let step = 0; step < 5; step++) {
        for (let index = 0; index < maxItemsPerStep; index++) {
          built = built.withItem(step, '1055');
        }
      }

      expect(built.totalItems).toBe(maxTotalItems);
      expect(built.canAddItem(5)).toBe(false);
      expect(built.withItem(5, '1055')).toBe(built);
    });

    it('removes and reorders the items of a step', () => {
      const built = order(['a', 'b', 'c']);

      expect(itemsOf(built.withoutItem({ step: 0, index: 1 }))).toEqual(['a', 'c']);
      expect(itemsOf(built.movingItem({ step: 0, index: 2 }, -1))).toEqual(['a', 'c', 'b']);
      expect(built.movingItem({ step: 0, index: 0 }, -1)).toBe(built);
      expect(built.movingItem({ step: 1, index: 0 }, 1)).toBe(built);
      expect(built.withoutItem({ step: 0, index: 7 })).toBe(built);
    });
  });

  describe('drop semantics (insertion indexes)', () => {
    it('moves a step to an insertion point, counting its own removal', () => {
      const built = order(['a'], ['b'], ['c']);

      expect(firstItems(built.movingStepTo(0, 3))).toEqual(['b', 'c', 'a']);
      expect(firstItems(built.movingStepTo(2, 0))).toEqual(['c', 'a', 'b']);
      // Dropping right before or right after itself changes nothing.
      expect(built.movingStepTo(1, 1)).toBe(built);
      expect(built.movingStepTo(1, 2)).toBe(built);
      expect(built.movingStepTo(9, 0)).toBe(built);
    });

    it('reorders the items of a step to an insertion point', () => {
      const built = order(['a', 'b', 'c']);

      expect(itemsOf(built.movingItemTo({ step: 0, index: 0 }, 3))).toEqual(['b', 'c', 'a']);
      expect(itemsOf(built.movingItemTo({ step: 0, index: 2 }, 0))).toEqual(['c', 'a', 'b']);
      expect(itemsOf(built.movingItemTo({ step: 0, index: 1 }, 99))).toEqual(['a', 'c', 'b']);
      expect(built.movingItemTo({ step: 0, index: 1 }, 1)).toBe(built);
      expect(built.movingItemTo({ step: 5, index: 0 }, 1)).toBe(built);
    });

    it('inserts an item at a clamped place, within the caps', () => {
      const built = order(['a', 'c']);

      expect(itemsOf(built.insertingItem({ step: 0, index: 1 }, 'b'))).toEqual(['a', 'b', 'c']);
      expect(itemsOf(built.insertingItem({ step: 0, index: -5 }, 'z'))).toEqual(['z', 'a', 'c']);
      expect(itemsOf(built.insertingItem({ step: 0, index: 99 }, 'z'))).toEqual(['a', 'c', 'z']);

      const head = { step: 0, index: 0 };
      let full = order([]);
      for (let index = 0; index < maxItemsPerStep; index++) {
        full = full.insertingItem(head, 'x');
      }
      expect(full.insertingItem(head, 'x')).toBe(full);
    });

    it('transfers an item to another step at its insertion point', () => {
      const next = order(['a', 'b'], ['c']).transferringItem(
        { step: 0, index: 1 },
        { step: 1, index: 0 },
      );

      expect(itemsOf(next, 0)).toEqual(['a']);
      expect(itemsOf(next, 1)).toEqual(['b', 'c']);
    });

    it('reorders when the transfer stays in its step', () => {
      const next = order(['a', 'b', 'c']).transferringItem(
        { step: 0, index: 0 },
        { step: 0, index: 3 },
      );

      expect(itemsOf(next)).toEqual(['b', 'c', 'a']);
    });

    it('refuses a transfer into a full step and nonsense places', () => {
      const full = order(
        Array.from({ length: maxItemsPerStep }, () => 'x'),
        ['y'],
      );

      expect(full.transferringItem({ step: 1, index: 0 }, { step: 0, index: 0 })).toBe(full);
      expect(full.transferringItem({ step: 5, index: 0 }, { step: 0, index: 0 })).toBe(full);
      expect(full.transferringItem({ step: 1, index: 9 }, { step: 0, index: 0 })).toBe(full);
    });

    it('may empty the source step: the API asks for an item per step on save', () => {
      const next = order(['a'], ['b']).transferringItem(
        { step: 0, index: 0 },
        { step: 1, index: 1 },
      );

      expect(itemsOf(next, 0)).toEqual([]);
      expect(itemsOf(next, 1)).toEqual(['b', 'a']);
    });

    it('lets a full step reorder its own items, never take another', () => {
      const full = order(
        Array.from({ length: maxItemsPerStep }, () => 'x'),
        ['y'],
      );

      expect(full.canReceiveItem(0, { step: 0, index: 2 })).toBe(true);
      expect(full.canReceiveItem(0, { step: 1, index: 0 })).toBe(false);
      expect(full.canReceiveItem(1, { step: 0, index: 0 })).toBe(true);
      expect(full.canReceiveItem(7, { step: 0, index: 0 })).toBe(false);
    });
  });

  describe('restingIndex (the position announced to screen readers)', () => {
    it('is where the entry lands, out-of-range drops included', () => {
      const built = order(['a', 'b', 'c']);
      const drops: [from: number, insert: number][] = [
        [0, 3],
        [2, 0],
        [1, 99],
        [1, -4],
        [0, 0],
        [2, 3],
      ];

      for (const [from, insert] of drops) {
        const moved = built.movingItemTo({ step: 0, index: from }, insert);
        const resting = PurchaseOrder.restingIndex(from, insert, 3);
        expect(itemsOf(moved)[resting]).toBe(itemsOf(built)[from]);
      }
    });

    it('is where a dragged step lands', () => {
      const built = order(['a'], ['b'], ['c']);
      const moved = built.movingStepTo(0, 3);

      expect(moved.steps[PurchaseOrder.restingIndex(0, 3, 3)].items[0]).toBe('a');
    });
  });

  describe('gold', () => {
    const prices: Record<string, number> = { '1055': 450, '3006': 1100 };
    const goldOf = (itemId: string): number | null => prices[itemId] ?? null;

    it('sums a step, a ghost counting for nothing', () => {
      expect(order(['1055', '3006', 'gone']).stepGold(0, goldOf)).toBe(1550);
    });

    it('sums the whole build', () => {
      expect(order(['1055'], ['3006', '3006']).gold(goldOf)).toBe(2650);
    });
  });

  it('hands the steps over as the API takes them', () => {
    const built = order(['1055']).patchingStep(0, { note: 'early' });

    expect(built.toSteps()).toEqual([{ label: 'Step 1', note: 'early', items: ['1055'] }]);
  });
});
