import type { Routes } from '@angular/router';
import type { DonationOutcome } from './outcome/donation-outcome-page';
import { resolveDonationOptions } from './resolve-donation-options';

const outcomePage = () =>
  import('./outcome/donation-outcome-page').then((m) => m.DonationOutcomePage);

/**
 * The donation page, `/{locale}/donate`, and the two pages Stripe returns to, all rendered
 * by the server; the return pages are kept out of the index by their head.
 */
export const DONATE_ROUTES: Routes = [
  {
    path: '',
    resolve: { options: resolveDonationOptions },
    loadComponent: () => import('./donate-page').then((m) => m.DonatePage),
  },
  {
    path: 'success',
    data: { outcome: 'success' satisfies DonationOutcome },
    loadComponent: outcomePage,
  },
  {
    path: 'cancel',
    data: { outcome: 'cancel' satisfies DonationOutcome },
    loadComponent: outcomePage,
  },
];
