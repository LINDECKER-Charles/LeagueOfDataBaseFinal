import type { PrerenderEpochVolume } from '../../../../../../../core/api/generated/models/prerender-epoch-volume';
import { prerenderReport } from '../../../../../testing/fixtures/prerender-report';
import { epochChoices } from './epoch-choices';

const SERVED = prerenderReport().storage.epochs[0];

function withEpochs(epochs: PrerenderEpochVolume[], servedEpoch = 2) {
  return prerenderReport({ servedEpoch, storage: { ...prerenderReport().storage, epochs } });
}

describe('epochChoices', () => {
  it('offers the served epoch, then a new one costing as much once complete', () => {
    expect(epochChoices(withEpochs([SERVED]))).toEqual([
      { kind: 'served', epoch: 2, share: null, monthlyCost: 0, currency: 'EUR' },
      { kind: 'new', epoch: 3, share: null, monthlyCost: 0.0742, currency: 'EUR' },
    ]);
  });

  it('offers to continue a standby epoch, with its coverage', () => {
    const standby: PrerenderEpochVolume = {
      ...SERVED,
      epoch: 3,
      state: 'standby',
      isWriting: false,
      coverage: { ...SERVED.coverage!, expectedUnits: 1_000, completeUnits: 642 },
    };

    const choices = epochChoices(withEpochs([standby, SERVED]));

    expect(choices.map((choice) => [choice.kind, choice.epoch])).toEqual([
      ['served', 2],
      ['standby', 3],
      ['new', 4],
    ]);
    expect(choices[1].share).toBeCloseTo(0.642);
  });

  it('numbers the new epoch past every epoch of the store, a retired one included', () => {
    const retired: PrerenderEpochVolume = { ...SERVED, epoch: 5, state: 'retired', coverage: null };

    expect(epochChoices(withEpochs([retired, SERVED])).at(-1)?.epoch).toBe(6);
    expect(epochChoices(withEpochs([], 7)).map((choice) => choice.epoch)).toEqual([7, 8]);
  });
});
