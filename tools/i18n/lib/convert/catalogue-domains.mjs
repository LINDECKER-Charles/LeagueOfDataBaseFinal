/**
 * Where each Symfony translation domain lands. `messages` is the root catalogue
 * (`public/i18n/<locale>.json`), the others are Transloco scopes
 * (`public/i18n/<scope>/<locale>.json`). `email.*` renders server-side mail: L4.3 takes it
 * from the YAML into the server resources, so it never ships to the browser.
 */
export const CATALOGUE_DOMAINS = Object.freeze({
  messages: { scope: null, excludedKeys: ['email'] },
  seo: { scope: 'seo', excludedKeys: [] },
  about: { scope: 'about', excludedKeys: [] },
  api: { scope: 'api', excludedKeys: [] },
});
