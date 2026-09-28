import { readServerSettings } from './read-server-settings';

describe('readServerSettings', () => {
  it('runs locally without any variable', () => {
    expect(readServerSettings({}, true)).toEqual({
      port: 4000,
      allowedHosts: ['localhost'],
      trustProxyHeaders: undefined,
      apiOrigin: 'http://api:8080',
      selfOrigin: 'http://127.0.0.1:4000',
    });
  });

  it('reads the deployment variables', () => {
    const settings = readServerSettings(
      {
        PORT: '4100',
        LODB_ALLOWED_HOSTS: 'league-of-data-base.com, next.league-of-data-base.com ,',
        LODB_TRUST_PROXY_HEADERS: 'x-forwarded-proto,x-forwarded-host',
        LODB_API_ORIGIN: 'http://api:8080/',
      },
      true,
    );

    expect(settings).toEqual({
      port: 4100,
      allowedHosts: ['league-of-data-base.com', 'next.league-of-data-base.com'],
      trustProxyHeaders: ['x-forwarded-proto', 'x-forwarded-host'],
      apiOrigin: 'http://api:8080',
      selfOrigin: 'http://127.0.0.1:4100',
    });
  });

  it('has no loopback origin when another server hosts the handler', () => {
    expect(readServerSettings({}, false).selfOrigin).toBeNull();
  });

  it('treats empty lists as unset', () => {
    const settings = readServerSettings({ LODB_ALLOWED_HOSTS: ' , ' }, true);

    expect(settings.allowedHosts).toEqual(['localhost']);
  });

  it.each(['0', '65536', '4000.5', 'http'])('refuses PORT=%s', (port) => {
    expect(() => readServerSettings({ PORT: port }, true)).toThrow(/PORT/);
  });

  it.each(['http://api:8080/api', 'http://api:8080?x=1', 'api:8080/v1', 'not a url'])(
    'refuses LODB_API_ORIGIN=%s',
    (origin) => {
      expect(() => readServerSettings({ LODB_API_ORIGIN: origin }, true)).toThrow();
    },
  );
});
