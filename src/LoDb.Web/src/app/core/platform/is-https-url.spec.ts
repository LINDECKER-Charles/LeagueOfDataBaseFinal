import { isHttpsUrl } from './is-https-url';

describe('isHttpsUrl', () => {
  it.each(['https://www.leagueoflegends.com/', 'HTTPS://example.com/a?b=c#d'])(
    'lets %s open outside the application',
    (url) => {
      expect(isHttpsUrl(url)).toBe(true);
    },
  );

  it.each([
    'http://www.leagueoflegends.com/',
    'javascript:alert(1)',
    'data:text/html,x',
    'file:///etc/passwd',
    'intent://scan#Intent;end',
    'champions',
    '',
  ])('keeps %s in', (url) => {
    expect(isHttpsUrl(url)).toBe(false);
  });
});
