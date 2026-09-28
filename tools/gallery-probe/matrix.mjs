// What the probe walks: every identity in both writing directions, at the widths of the
// L3.2 acceptance. The expected values mirror src/app/core/theme (themes, identities).

/** Theme name and the `theme-color` it paints the browser chrome with. */
export const THEMES = [
  { name: 'hextech', browserColor: '#010a13' },
  { name: 'zaun', browserColor: '#030706' },
  { name: 'noxus', browserColor: '#08090b' },
  { name: 'spirit-blossom', browserColor: '#07070e' },
];

/** One left-to-right and one right-to-left locale. */
export const LOCALES = [
  { locale: 'en', dir: 'ltr' },
  { locale: 'ar', dir: 'rtl' },
];

/** Capture widths; the first is the narrowest supported phone, checked for overflow. */
export const WIDTHS = [320, 390, 768, 1440];

export const NARROWEST = WIDTHS[0];

/** Phones and tablets are emulated as touch devices, so the layout viewport can widen. */
export const MOBILE_UP_TO = 768;

export const VIEWPORT_HEIGHT = 900;

export const GALLERY_PATH = '/dev/gallery';
