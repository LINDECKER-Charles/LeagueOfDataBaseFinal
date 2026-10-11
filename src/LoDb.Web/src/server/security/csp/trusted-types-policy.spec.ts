import { trustedTypesPolicy } from './trusted-types-policy';

describe('trustedTypesPolicy', () => {
  it("requires Trusted Types for scripts, from the site's own policies only", () => {
    expect(trustedTypesPolicy().split('; ')).toEqual([
      "require-trusted-types-for 'script'",
      'trusted-types angular angular#unsafe-bypass lodb#worker lodb#json-ld',
      'report-uri /api/csp-reports',
      'report-to csp',
    ]);
  });
});
