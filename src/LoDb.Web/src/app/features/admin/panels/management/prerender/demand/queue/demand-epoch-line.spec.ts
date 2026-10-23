import { prerenderDemand } from '../../../../../testing/fixtures/prerender-demand';
import type { AdminText } from '../../../../../shared/inject-admin-text';
import { demandEpochLine } from './demand-epoch-line';

// Writes each text as its key and its values, to see which sentences were chosen.
const TEXTS: AdminText = {
  text: (key, params) => `${key}${params ? ` ${JSON.stringify(params)}` : ''}`,
  term: (_group, name) => name,
};

describe('demandEpochLine', () => {
  it('counts the pages written, those still held, and those a pass wrote again', () => {
    const line = demandEpochLine(prerenderDemand(), TEXTS);

    expect(line).toContain('"written":1830');
    expect(line).toContain('"kept":1790');
    expect(line).toContain('"size":"179.00 MB"');
    expect(line).toContain('epoch_superseded {"superseded":40');
  });

  it('leaves out the rewritten pages while a pass wrote none again', () => {
    const line = demandEpochLine(prerenderDemand({ superseded: 0 }), TEXTS);

    expect(line).not.toContain('epoch_superseded');
  });

  it('says the queue wrote nothing yet into the served epoch', () => {
    const line = demandEpochLine(prerenderDemand({ servedEpochPages: 0, superseded: 0 }), TEXTS);

    expect(line).toBe('prerender.demand.queue.epoch_empty {"epoch":1}');
  });
});
