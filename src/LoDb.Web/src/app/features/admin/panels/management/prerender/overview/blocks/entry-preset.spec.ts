import { entryPreset } from './entry-preset';

describe('entryPreset', () => {
  it('picks nothing from the head of the page, in the served epoch', () => {
    expect(entryPreset('header', null)).toEqual({
      origin: 'header',
      versions: [],
      priority: [],
      locales: null,
      pageTypes: null,
      epoch: null,
      skipComplete: true,
      fromPass: null,
    });
  });

  it('takes the gaps of the epoch to complete', () => {
    expect(entryPreset('idle', null)).toMatchObject({ origin: 'idle', versions: 'incomplete' });
    expect(entryPreset('complete-epoch', 3)).toMatchObject({
      origin: 'complete-epoch',
      versions: 'incomplete',
      epoch: 3,
      skipComplete: true,
    });
  });
});
