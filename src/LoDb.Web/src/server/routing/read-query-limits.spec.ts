import { readQueryLimits } from './read-query-limits';

describe('readQueryLimits', () => {
  it('keeps 16 parameters of 200 characters by default', () => {
    expect(readQueryLimits({})).toEqual({ maxParams: 16, maxParamLength: 200 });
  });

  it('reads the deployment variables', () => {
    const env = { LODB_QUERY_MAX_PARAMS: '8', LODB_QUERY_MAX_PARAM_LENGTH: '500' };

    expect(readQueryLimits(env)).toEqual({ maxParams: 8, maxParamLength: 500 });
  });

  it('refuses a bound that is not a positive integer', () => {
    expect(() => readQueryLimits({ LODB_QUERY_MAX_PARAMS: '0' })).toThrow(/LODB_QUERY_MAX_PARAMS/);
  });
});
