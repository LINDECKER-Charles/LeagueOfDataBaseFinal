import { checkoutNotice } from './checkout-status';

describe('checkoutNotice', () => {
  it.each([
    ['pack_success', 'success'],
    ['plan_success', 'success'],
    ['cancelled', 'muted'],
  ])('announces the return %s', (status, tone) => {
    expect(checkoutNotice(status)).toEqual({ tone, key: `api.portal.status.${status}` });
  });

  it.each([null, '', 'api.portal.title', 'PACK_SUCCESS'])('ignores %j', (status) => {
    expect(checkoutNotice(status)).toBeNull();
  });
});
