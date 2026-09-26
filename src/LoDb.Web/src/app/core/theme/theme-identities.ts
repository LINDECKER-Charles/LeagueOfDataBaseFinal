import type { ThemeIdentity } from './theme-identity';
import type { Theme } from './themes';

/** Presentation of the four identities (legacy App\Service\Client\Theme). */
export const THEME_IDENTITIES: Readonly<Record<Theme, ThemeIdentity>> = {
  hextech: { label: 'Hextech', origin: 'Piltover', browserColor: '#010a13' },
  zaun: { label: 'Zaun', origin: 'Zaun', browserColor: '#030706' },
  noxus: { label: 'Noxus', origin: 'Noxus', browserColor: '#08090b' },
  'spirit-blossom': { label: 'Spirit Blossom', origin: 'Ionia', browserColor: '#07070e' },
};
