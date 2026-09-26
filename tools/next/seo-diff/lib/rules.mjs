// The differences the diff knows the reason of. A rule never hides a difference: the report
// lists each one with the rule that explains it. Three kinds:
//   decision    wanted by an ADR or the plan;
//   correction  a defect of production that the rewrite no longer has;
//   defect      a regression of the rewrite, recorded for the milestone of lot 3.
// A difference no rule explains is reported as unexplained.

import { decodeEntities } from './head.mjs';

const LIST_PAGE_SIZE = 12;
const PAGE_KEY = /^(?:home|page:|list:|detail:|asset:)/;

function isLocalized(pair) {
  return pair.nextLocale !== null && pair.nextLocale !== 'en';
}

function isText(value) {
  return typeof value === 'string' && !PAGE_KEY.test(value);
}

function indexOf(path = '', collection) {
  const match = path.match(new RegExp(`${collection}\\[(\\d+)\\]`));
  return match === null ? null : Number(match[1]);
}

/** The rules, in the order they are tried. `applies(field, difference, pair)`. */
export const RULES = [
  {
    id: 'locale-in-url',
    kind: 'decision',
    reason:
      "ADR 0005 : la locale d'interface est dans l'URL. La prod n'a qu'une URL par page et sert " +
      "ses textes SEO en anglais aux robots (locale de session ou de cookie) ; `?lang=` n'y " +
      'change que la langue des données. La cible traduit titre, description, noms du fil ' +
      "d'Ariane et du graphe du site, et `inLanguage`.",
    applies: (field, difference, pair) =>
      isLocalized(pair) &&
      (field === 'title' ||
        field === 'description' ||
        (field === 'fields' && isText(difference.next))),
  },
  {
    id: 'list-first-page',
    kind: 'decision',
    reason:
      "ADR 0005 (listes) et L3.4 : l'`ItemList` décrit la première page rendue par le serveur, " +
      `${LIST_PAGE_SIZE} cartes, dans la limite de 20 ; la prod listait les 20 premières ` +
      'entrées du jeu de données complet.',
    applies: (field, difference) =>
      field === 'fields' &&
      (difference.path === 'ItemList.numberOfItems' ||
        (difference.next === undefined &&
          (indexOf(difference.path, 'itemListElement') ?? -1) >= LIST_PAGE_SIZE)),
  },
  {
    id: 'item-list-index-urls',
    kind: 'correction',
    reason:
      "La prod écrit dans l'`ItemList` des objets l'indice de la carte au lieu de l'id " +
      "(`/object/0`, `/object/1`…) : des liens morts. La cible pointe l'URL canonique de l'objet.",
    applies: (field, difference) =>
      field === 'fields' &&
      difference.path.endsWith('.url') &&
      String(difference.prod).startsWith(
        `detail:items:${indexOf(difference.path, 'itemListElement')}@`,
      ),
  },
  {
    id: 'json-ld-html-escaping',
    kind: 'correction',
    reason:
      'La prod échappe en HTML le texte du JSON-LD (`&amp;`, `&#039;`) : les robots lisent ' +
      '« K&#039;Sante ». La cible écrit le texte brut, échappé pour le seul contexte du script.',
    applies: (field, difference) =>
      field === 'fields' &&
      typeof difference.prod === 'string' &&
      difference.prod !== difference.next &&
      decodeEntities(difference.prod) === difference.next,
  },
  {
    id: 'item-description-fallback',
    kind: 'correction',
    reason:
      "Pour un `plaintext` vide (objets LoL Classic), l'opérateur `??` de PHP garde la chaîne " +
      'vide et la prod retire la description ; la cible retombe sur la description de Data Dragon.',
    applies: (field, difference) =>
      field === 'fields' &&
      difference.path === 'VideoGame.gameItem.description' &&
      difference.prod === undefined,
  },
  {
    id: 'prerendered-data-page',
    kind: 'decision',
    reason:
      'ADR 0005 : `/about/data` est prérendue au build, sans appel à l’API ; le patch courant ' +
      "n'y est donc ni dans la description, ni dans le nom ou la version du `Dataset` : la page " +
      'le remplit dans le navigateur.',
    applies: (field, difference, pair) =>
      pair.nextKey === 'page:about/data' &&
      (field === 'description' ||
        ['Dataset.description', 'Dataset.name', 'Dataset.version'].includes(difference.path)),
  },
  {
    id: 'dataset-languages',
    kind: 'defect',
    reason:
      "`Dataset.inLanguage` liste les 21 locales du site au lieu des 28 langues de Data Dragon " +
      "(la prod les écrivait au format `fr_FR`, hors BCP 47). Écart au « JSON-LD conservé à " +
      "l'identique » de l'ADR 0005, à trancher par L3.10 " +
      '(`features/editorial/about/about-data-page.ts`) : langues Data Dragon en BCP 47.',
    applies: (field, difference, pair) =>
      field === 'fields' &&
      pair.nextKey === 'page:about/data' &&
      difference.path.startsWith('Dataset.inLanguage['),
  },
];

/** The first rule that explains a difference, or null. */
export function ruleFor(field, difference, pair) {
  return RULES.find((rule) => rule.applies(field, difference, pair)) ?? null;
}
