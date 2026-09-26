import { languageLabel } from './language-label';
import { versionChoices } from './version-choices';

describe('versionChoices', () => {
  const versions = ['16.19.1', '15.14.1'];

  it('offers the listed patches when the pinned one is among them', () => {
    expect(versionChoices(versions, '15.14.1')).toBe(versions);
  });

  it('keeps a delisted pinned patch at the end, so the build stays editable on it', () => {
    expect(versionChoices(versions, '9.1.1')).toEqual(['16.19.1', '15.14.1', '9.1.1']);
  });

  it('adds nothing for a build pinned to no patch yet', () => {
    expect(versionChoices(versions, '')).toBe(versions);
  });
});

describe('languageLabel', () => {
  it('names a language in itself', () => {
    expect(languageLabel('fr_FR')).toBe('Français (France)');
  });

  it('falls back to the code when Intl cannot name it', () => {
    expect(languageLabel('not a language')).toBe('not a language');
  });
});
