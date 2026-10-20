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
    expect(passRequestOf(form({ epoch: ' ', oldest: '', priority: '\n' }))).toEqual({
      epoch: null,
      oldest: null,
      newest: null,
      pageType: null,
      locale: null,
      priority: null,
    });
  });

  it('reads a pasted list of versions in its order, each once', () => {
    const request = passRequestOf(
      form({ epoch: '2', newest: ' 16.18.1 ', priority: '16.18.1\n16.17.1; 15.1.1,16.18.1' }),
    );

    expect(request.epoch).toBe(2);
    expect(request.newest).toBe('16.18.1');
    expect(request.priority).toEqual(['16.18.1', '16.17.1', '15.1.1']);
  });
});
