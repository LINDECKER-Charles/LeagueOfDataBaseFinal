import { authHeaders, curlSamples, usageSample } from './api-samples';
import { pricingRows } from './pricing-rows';

describe('api samples', () => {
  it('shows the key in both headers, never a whole secret', () => {
    expect(authHeaders('lodb_')).toBe('Authorization: Bearer lodb_…\nX-Api-Key: lodb_…');
  });

  it('calls the configured base URL', () => {
    const curl = curlSamples('https://api.example.test', 'lodb_');

    expect(curl).toContain('"https://api.example.test/v1/trends/champions?range=30d"');
    expect(curl).toContain('"https://api.example.test/v1/usage"');
  });

  it('answers /v1/usage with the free plan of the API, as /v1 writes it', () => {
    expect(JSON.parse(usageSample({ monthlyQuota: 500, ratePerMinute: 10 }))).toEqual({
      plan: 'free',
      monthly_quota: 500,
      used_this_month: 137,
      remaining_this_month: 363,
      credits_balance: 0,
      rate_limit_per_min: 10,
    });
  });
});

describe('pricingRows', () => {
  it('lists the free plan, the packs at the credits rate, then the plans', () => {
    const rows = pricingRows(
      {
        baseUrl: 'https://league-of-data-base.com',
        keyPrefix: 'lodb_',
        freePlan: { monthlyQuota: 500, ratePerMinute: 10 },
        creditsRatePerMinute: 60,
        packs: [{ code: 'small', priceCents: 500, requests: 5_000 }],
        plans: [
          {
            code: 'annual',
            interval: 'year',
            monthlyQuota: 20_000,
            priceCents: 9_900,
            ratePerMinute: 120,
          },
        ],
      },
      (count) => `#${count}`,
    );

    expect(rows).toEqual([
      expect.objectContaining({ code: 'free', price: 0, period: null, count: '#500', rate: 10 }),
      expect.objectContaining({
        name: 'api.pack.small',
        price: 5,
        volume: 'api.portal.billing.requests_once',
        rate: 60,
      }),
      expect.objectContaining({
        name: 'api.plan.annual',
        price: 99,
        period: 'api.portal.billing.per_year',
        count: '#20000',
        rate: 120,
      }),
    ]);
  });
});
