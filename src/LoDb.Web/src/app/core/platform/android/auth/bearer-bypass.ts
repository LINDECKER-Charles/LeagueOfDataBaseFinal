import { HttpContextToken } from '@angular/common/http';

/**
 * Marks the token calls themselves (sign-in, refresh, Google exchange): the `bearer` strategy
 * sends them as they are. Authorizing them would send a stale token with a sign-in, and
 * answer a refused password (401) with a refresh.
 */
export const BEARER_BYPASS = new HttpContextToken<boolean>(() => false);
