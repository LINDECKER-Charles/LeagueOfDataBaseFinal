import type { AccountUser } from '../../api/generated/models/account-user';

/** A signed-in account for the specs, with the fields a test cares about overridden. */
export function accountUser(overrides: Partial<AccountUser> = {}): AccountUser {
  return {
    id: 7,
    email: 'faker@example.com',
    username: 'Faker',
    riotTagline: 'KR1',
    emailVerified: true,
    hasPassword: true,
    isSupporter: false,
    multiFactor: false,
    twoFactorEnabled: false,
    roles: [],
    ...overrides,
  };
}
