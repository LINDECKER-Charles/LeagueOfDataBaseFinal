// The five pages measured (L3.13): one per kind of public page, each with its production
// equivalent, measured for information only.

/** `next` is a path of the stack (ADR 0005), `prod` the same page on production. */
export const PAGES = [
  { name: 'Accueil', next: '/en/', prod: '/' },
  { name: 'Liste des champions', next: '/en/champions', prod: '/champions' },
  { name: 'Champion (Annie)', next: '/en/champions/Annie', prod: '/champion/Annie' },
  { name: 'Objet (Infinity Edge)', next: '/en/items/3031-infinity-edge', prod: '/object/3031' },
  { name: 'À propos (prérendue)', next: '/en/about', prod: '/about' },
];
