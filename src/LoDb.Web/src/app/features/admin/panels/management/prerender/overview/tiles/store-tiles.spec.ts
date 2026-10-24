import type { AdminText } from '../../../../../shared/inject-admin-text';
import { prerenderReport } from '../../../../../testing/fixtures/prerender-report';
import { coverageTileOf } from './coverage-tile-of';
import { storageTileOf } from './storage-tile-of';

// Texts read as `key {params}`, so a spec sees which key is chosen and what it is given.
const TEXTS: AdminText = {
  text: (key, params) => (params ? `${key} ${JSON.stringify(params)}` : key),
  term: (_group, name) => name,
};
const COVERAGE = 'prerender.overview.views.coverage';
// The fixture's served epoch 2 covers 8 274 of 8 442 units: 391 of its 402 patches whole.
const REPORT = prerenderReport();

describe('coverageTileOf', () => {
  it('gives the share of the units the served epoch covers, and its complete patches', () => {
    expect(coverageTileOf(REPORT, TEXTS)).toEqual({
      label: `${COVERAGE}.label`,
      value: '98 %',
      sub: `${COVERAGE}.sub {"epoch":2,"complete":"391","total":402}`,
      tone: null,
    });
  });

  it('lights a green dot and names the epoch ready to be served', () => {
    expect(coverageTileOf(prerenderReport({ readyEpoch: 3 }), TEXTS)).toMatchObject({
      sub: `${COVERAGE}.sub_ready {"epoch":2,"ready":3}`,
      tone: 'good',
    });
  });

  it('reads a dash while the served epoch counts no unit', () => {
    const epochs = REPORT.storage.epochs.map((epoch) => ({ ...epoch, coverage: null }));
    const report = prerenderReport({ storage: { ...REPORT.storage, epochs } });

    expect(coverageTileOf(report, TEXTS)).toMatchObject({
      value: '—',
      sub: `${COVERAGE}.sub_none {"epoch":2}`,
    });
  });
});

describe('storageTileOf', () => {
  it('gives the volume of the store in decimal units, its monthly cost and its pages', () => {
    expect(storageTileOf(REPORT, TEXTS)).toEqual({
      label: 'prerender.overview.views.storage.label',
      value: '9.26 GB',
      sub: 'prerender.overview.views.storage.sub {"cost":"0,07 €","pages":91230}',
      tone: null,
    });
  });
});
