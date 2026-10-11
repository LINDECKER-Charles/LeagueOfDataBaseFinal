import { readRenderLimits } from './admission/read-render-limits';
import { readServerSettings } from './read-server-settings';

const DEFAULT_RENDER_LIMITS = readRenderLimits({});
const DEFAULT_RENDER_TIMEOUTS = { deadlineMs: 20_000, apiCallMs: 10_000 };
const DEFAULT_QUERY_LIMITS = { maxParams: 16, maxParamLength: 200 };

describe('readServerSettings', () => {
  it('runs locally without any variable', () => {
    expect(readServerSettings({}, true)).toEqual({
      port: 4000,
      metricsPort: 9464,
      allowedHosts: ['localhost'],
      trustProxyHeaders: undefined,
      stackNetworks: [],
      apiOrigin: 'http://lodb-api:8080',
      selfOrigin: 'http://127.0.0.1:4000',
      renderLimits: DEFAULT_RENDER_LIMITS,
      renderTimeouts: DEFAULT_RENDER_TIMEOUTS,
      queryLimits: DEFAULT_QUERY_LIMITS,
    });
  });

  it('reads the deployment variables', () => {
    const settings = readServerSettings(
      {
        PORT: '4100',
        LODB_METRICS_PORT: '9465',
        LODB_ALLOWED_HOSTS: 'league-of-data-base.com, next.league-of-data-base.com ,',
        LODB_TRUST_PROXY_HEADERS: 'x-forwarded-proto,x-forwarded-host',
        LODB_STACK_NETWORKS: '10.231.2.0/24, fd12:3456::/64',
        LODB_API_ORIGIN: 'http://lodb-api.internal:8080/',
        LODB_PINNED_RENDERS_PER_MINUTE: '30',
        LODB_RENDER_DEADLINE_SECONDS: '25',
        LODB_QUERY_MAX_PARAMS: '8',
      },
      true,
    );

    expect(settings).toEqual({
      port: 4100,
      metricsPort: 9465,
      allowedHosts: ['league-of-data-base.com', 'next.league-of-data-base.com'],
      trustProxyHeaders: ['x-forwarded-proto', 'x-forwarded-host'],
      stackNetworks: ['10.231.2.0/24', 'fd12:3456::/64'],
      apiOrigin: 'http://lodb-api.internal:8080',
      selfOrigin: 'http://127.0.0.1:4100',
      renderLimits: { ...DEFAULT_RENDER_LIMITS, pinnedPerMinute: 30 },
      renderTimeouts: { ...DEFAULT_RENDER_TIMEOUTS, deadlineMs: 25_000 },
      queryLimits: { ...DEFAULT_QUERY_LIMITS, maxParams: 8 },
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

  it.each(['0', '65536', 'metrics'])('refuses LODB_METRICS_PORT=%s', (port) => {
    expect(() => readServerSettings({ LODB_METRICS_PORT: port }, true)).toThrow(
      /LODB_METRICS_PORT must be a TCP port/,
    );
  });

  it('refuses a metrics port that is the site port', () => {
    expect(() => readServerSettings({ PORT: '9464' }, true)).toThrow(/must differ from PORT/);
    expect(() => readServerSettings({ LODB_METRICS_PORT: '4000' }, true)).toThrow(
      /must differ from PORT/,
    );
  });

  it.each(['http://api:8080/api', 'http://api:8080?x=1', 'api:8080/v1', 'not a url'])(
    'refuses LODB_API_ORIGIN=%s',
    (origin) => {
      expect(() => readServerSettings({ LODB_API_ORIGIN: origin }, true)).toThrow();
    },
  );
});
