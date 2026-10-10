import { readRenderLimits } from './admission/read-render-limits';
import { readServerSettings } from './read-server-settings';

const DEFAULT_RENDER_LIMITS = readRenderLimits({});
const DEFAULT_RENDER_TIMEOUTS = { deadlineMs: 20_000, apiCallMs: 10_000 };
const DEFAULT_QUERY_LIMITS = { maxParams: 16, maxParamLength: 200 };

describe('readServerSettings', () => {
  it('runs locally without any variable', () => {
    expect(readServerSettings({}, true)).toEqual({
      port: 4000,
      allowedHosts: ['localhost'],
      trustProxyHeaders: undefined,
      apiOrigin: 'http://api:8080',
      selfOrigin: 'http://127.0.0.1:4000',
      frontBuildsFolder: null,
      renderLimits: DEFAULT_RENDER_LIMITS,
      renderTimeouts: DEFAULT_RENDER_TIMEOUTS,
      queryLimits: DEFAULT_QUERY_LIMITS,
      htmlGzipLevel: 6,
    });
  });

  it('reads the deployment variables', () => {
    const settings = readServerSettings(
      {
        PORT: '4100',
        LODB_ALLOWED_HOSTS: 'league-of-data-base.com, next.league-of-data-base.com ,',
        LODB_TRUST_PROXY_HEADERS: 'x-forwarded-proto,x-forwarded-host',
        LODB_API_ORIGIN: 'http://api:8080/',
        LODB_FRONT_BUILDS_DIR: '/srv/front-builds',
        LODB_PINNED_RENDERS_PER_MINUTE: '30',
        LODB_RENDER_DEADLINE_SECONDS: '25',
        LODB_QUERY_MAX_PARAMS: '8',
        LODB_HTML_GZIP_LEVEL: '9',
      },
      true,
    );

    expect(settings).toEqual({
      port: 4100,
      allowedHosts: ['league-of-data-base.com', 'next.league-of-data-base.com'],
      trustProxyHeaders: ['x-forwarded-proto', 'x-forwarded-host'],
      apiOrigin: 'http://api:8080',
      selfOrigin: 'http://127.0.0.1:4100',
      frontBuildsFolder: '/srv/front-builds',
      renderLimits: { ...DEFAULT_RENDER_LIMITS, pinnedPerMinute: 30 },
      renderTimeouts: { ...DEFAULT_RENDER_TIMEOUTS, deadlineMs: 25_000 },
      queryLimits: { ...DEFAULT_QUERY_LIMITS, maxParams: 8 },
      htmlGzipLevel: 9,
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

  it.each(['srv/front-builds', './front-builds'])('refuses LODB_FRONT_BUILDS_DIR=%s', (folder) => {
    expect(() => readServerSettings({ LODB_FRONT_BUILDS_DIR: folder }, true)).toThrow(
      /LODB_FRONT_BUILDS_DIR/,
    );
  });

  it.each(['0', '10', '6.5', 'best'])('refuses LODB_HTML_GZIP_LEVEL=%s', (level) => {
    expect(() => readServerSettings({ LODB_HTML_GZIP_LEVEL: level }, true)).toThrow(
      /LODB_HTML_GZIP_LEVEL/,
    );
  });
});
