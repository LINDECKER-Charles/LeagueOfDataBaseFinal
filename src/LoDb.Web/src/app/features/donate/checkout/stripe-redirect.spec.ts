import { DOCUMENT, Injector } from '@angular/core';
import { StripeRedirect } from './stripe-redirect';

// A document whose location only records where it was sent.
function redirect() {
  const assign = vi.fn();
  const injector = Injector.create({
    providers: [
      { provide: DOCUMENT, useValue: { location: { assign } } },
      { provide: StripeRedirect },
    ],
  });
  return { stripe: injector.get(StripeRedirect), assign };
}

describe('StripeRedirect', () => {
  it('follows the secure page Stripe opened', () => {
    const { stripe, assign } = redirect();

    expect(stripe.go('https://checkout.stripe.com/c/pay/cs_test_1#fid')).toBe(true);
    expect(assign).toHaveBeenCalledExactlyOnceWith(
      'https://checkout.stripe.com/c/pay/cs_test_1#fid',
    );
  });

  it.each(['http://checkout.stripe.com/c/pay/cs_test_1', 'javascript:alert(1)', '/c/pay', ''])(
    'stays on the page for %j',
    (url) => {
      const { stripe, assign } = redirect();

      expect(stripe.go(url)).toBe(false);
      expect(assign).not.toHaveBeenCalled();
    },
  );
});
