import { passRequestOf } from './pass-request-of';

function form(fields: Readonly<Record<string, string>>): FormData {
  const data = new FormData();
  for (const [name, value] of Object.entries(fields)) {
    data.set(name, value);
  }
  return data;
}

describe('passRequestOf', () => {
  it('leaves every field it is not given unrestricted, the epoch the served one', () => {
    expect(passRequestOf(form({ epoch: ' ', versions: '', priority: '\n' }))).toEqual({
      epoch: null,
      versions: null,
      locales: null,
      pageTypes: null,
      priority: null,
      skipComplete: false,
    });
  });

  it('reads pasted lists of versions in their order, each once', () => {
    const request = passRequestOf(
      form({
        epoch: '2',
        versions: '16.18.1\n15.2.1 16.18.1',
        priority: '16.18.1\n16.17.1; 15.1.1,16.18.1',
      }),
    );

    expect(request.epoch).toBe(2);
    expect(request.versions).toEqual(['16.18.1', '15.2.1']);
    expect(request.priority).toEqual(['16.18.1', '16.17.1', '15.1.1']);
  });

  it('sends the one type and the one locale picked as lists of one', () => {
    const request = passRequestOf(form({ pageType: 'items', locale: 'zh-hans' }));

    expect(request.pageTypes).toEqual(['items']);
    expect(request.locales).toEqual(['zh-hans']);
  });
});
