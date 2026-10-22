import type { PrerenderPassView } from '../../../../../../core/api/generated/models/prerender-pass-view';
import { prerenderReport } from '../../../../testing/fixtures/prerender-report';
import { relaunchPreset } from './relaunch-preset';

const [RUNNING, CANCELLED] = prerenderReport().passes;
const PATCHES = ['16.19.1', '15.1.1', '14.24.1', '14.10.1', '14.9.1', '14.1.1', '13.24.1'];
const SOURCES = { scopeVersions: null, patches: PATCHES, servedEpoch: 2 };

const LISTED: PrerenderPassView = {
  ...RUNNING,
  id: 11,
  scope: {
    ...RUNNING.scope,
    versionCount: 3,
    newestListed: '16.18.1',
    oldestListed: '15.2.1',
    locales: ['en', 'fr'],
    pageTypes: ['items'],
    priority: ['16.12.1', '16.1.1'],
    skipComplete: true,
  },
};

describe('relaunchPreset', () => {
  it('takes the list of the complete read, with the locales, types and priorities', () => {
    const sources = { ...SOURCES, scopeVersions: ['16.18.1', '16.12.1', '15.2.1'] };

    expect(relaunchPreset(LISTED, sources)).toEqual({
      origin: 'relaunch',
      versions: ['16.18.1', '16.12.1', '15.2.1'],
      priority: ['16.12.1'],
      locales: ['en', 'fr'],
      pageTypes: ['items'],
      epoch: 2,
      skipComplete: false,
      pages: null,
      fromPass: 11,
    });
  });

  it('keeps the openers of a pass that rendered them alone', () => {
    const openers = { ...LISTED, scope: { ...LISTED.scope, pages: 'openers' } };

    expect(relaunchPreset(openers, SOURCES).pages).toBe('openers');
  });

  it('takes every older patch when the pass did', () => {
    const preset = relaunchPreset(RUNNING, SOURCES);

    expect(preset.versions).toBe('all');
    expect(preset.priority).toEqual(['16.19.1', '16.18.1']);
    expect(preset.locales).toBeNull();
  });

  it('turns a range from before the lists into the older patches it spans', () => {
    const preset = relaunchPreset({ ...CANCELLED, epoch: 2 }, SOURCES);

    expect(preset.versions).toEqual(['14.24.1', '14.10.1', '14.9.1', '14.1.1']);
    expect(preset.locales).toEqual(['fr']);
    expect(preset.pageTypes).toEqual(['items']);
  });

  it('relaunches into the served epoch a pass of an epoch retired since', () => {
    expect(relaunchPreset(CANCELLED, SOURCES).epoch).toBe(2);
    expect(relaunchPreset({ ...LISTED, epoch: 3 }, SOURCES).epoch).toBe(3);
  });
});
