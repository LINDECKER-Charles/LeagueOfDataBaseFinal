import { parseGoogleReturn } from './parse-google-return';

const REDIRECT = 'https://league-of-data-base.com/app/oauth/google';

describe('parseGoogleReturn', () => {
  it('reads the code and the state Google sent back', () => {
    expect(parseGoogleReturn(`${REDIRECT}?code=c-1&state=s-1&scope=email`, REDIRECT)).toEqual({
      state: 's-1',
      code: 'c-1',
      error: null,
    });
  });

  it('reads the error that replaces the code', () => {
    expect(parseGoogleReturn(`${REDIRECT}?error=access_denied&state=s-1`, REDIRECT)).toEqual({
      state: 's-1',
      code: null,
      error: 'access_denied',
    });
  });

  it.each([
    'https://league-of-data-base.com/fr/builds?code=c-1',
    'https://evil.example/app/oauth/google?code=c-1',
    'http://league-of-data-base.com/app/oauth/google?code=c-1',
    'not a url',
  ])('ignores any other link (%s)', (url) => {
    expect(parseGoogleReturn(url, REDIRECT)).toBeNull();
  });
});
