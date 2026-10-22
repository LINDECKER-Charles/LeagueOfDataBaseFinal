import type { PatchChanges } from '../../../../core/api/generated/models/patch-changes';
import type { PageContext } from '../../../../core/context/page-context';
import { buildHeadTags } from '../../../../core/seo/head/build-head-tags';
import type { HeadTags } from '../../../../core/seo/head/head-tags';
import { PATCHES_CATALOGUES } from '../../../../core/testing/patches-catalogues';
import { ROOT_CATALOGUES } from '../../../../core/testing/root-catalogues';
import { translateFrom } from '../../../../core/testing/translate-from';
import { patchChangesHeadOf } from './patch-changes-head-of';

const ORIGIN = 'https://league-of-data-base.com';
const TEXTS = { siteTitle: 'League of Data Base', siteDescription: 'LoL.', versionedSuffix: null };
const FR = translateFrom({ ...ROOT_CATALOGUES.fr, patches: PATCHES_CATALOGUES.fr });
const PINNED: PageContext = { locale: 'fr', version: '16.19.1', pinned: true, language: 'fr_FR' };
const IMAGE = { status: 'absent' } as const;

const CHANGES: PatchChanges = {
  version: '16.19.1',
  language: 'fr_FR',
  previousVersion: '16.18.1',
  hasChanges: true,
  groups: [
    {
      type: 'items',
      entries: [
        {
          id: '6610',
          name: 'Fendoir',
          image: IMAGE,
          kind: 'new',
          changedFields: [],
          link: { version: '16.19.1', path: 'items/6610-sundered-sky' },
        },
        {
          id: '3371',
          name: 'Lame en fusion',
          image: IMAGE,
          kind: 'retired',
          changedFields: [],
          link: { version: '11.5.1', path: 'items/3371-molten-edge' },
        },
      ],
    },
  ],
};

function headOf(context: PageContext, changes: PatchChanges = CHANGES): HeadTags {
  const page = patchChangesHeadOf({ context, details: changes }, FR);
  return buildHeadTags({ page, texts: TEXTS, origin: ORIGIN, locale: 'fr' });
}

function hrefs(head: HeadTags, rel: string): string[] {
  return head.links.filter((link) => link.rel === rel).map((link) => link.href);
}

describe('patchChangesHeadOf', () => {
  it('indexes a past patch that changed something under its own URL, named once', () => {
    const head = headOf(PINNED);

    expect(head.title).toBe('Changements du patch 16.19.1 — League Of Data Base');
    expect(hrefs(head, 'canonical')).toEqual([`${ORIGIN}/fr/16.19.1/changes`]);
    expect(hrefs(head, 'alternate')).toHaveLength(22);
    expect(head.metas.find((meta) => meta.key === 'robots')?.content).toBe('index, follow');
  });

  it('states the trail home › patch history › patch, then the pages it lists', () => {
    const [, breadcrumb, list] = headOf(PINNED).jsonLd;

    expect(breadcrumb).toMatchObject({
      '@type': 'BreadcrumbList',
      itemListElement: [
        { position: 1, name: 'Accueil', item: `${ORIGIN}/fr/` },
        { position: 2, name: 'Historique des patchs', item: `${ORIGIN}/fr/patches` },
        { position: 3, name: 'Changements du patch 16.19.1', item: `${ORIGIN}/fr/16.19.1/changes` },
      ],
    });
    expect(list).toMatchObject({
      '@type': 'ItemList',
      itemListElement: [
        { name: 'Fendoir', url: `${ORIGIN}/fr/16.19.1/items/6610-sundered-sky` },
        { name: 'Lame en fusion', url: `${ORIGIN}/fr/11.5.1/items/3371-molten-edge` },
      ],
    });
  });

  it('keeps the latest patch at its short URL', () => {
    const head = headOf({ ...PINNED, pinned: false });

    expect(hrefs(head, 'canonical')).toEqual([`${ORIGIN}/fr/changes`]);
  });

  it('keeps a patch that changed nothing out of the index, its links followed', () => {
    const head = headOf(PINNED, { ...CHANGES, hasChanges: false, groups: [] });

    expect(head.metas.find((meta) => meta.key === 'robots')?.content).toBe('noindex, follow');
    expect(head.metas.find((meta) => meta.key === 'description')?.content).toBe(
      'Le patch 16.19.1 de League of Legends n’a changé aucun champion, objet, rune ni sort ' +
        'd’invocateur par rapport au patch 16.18.1.',
    );
    expect(hrefs(head, 'canonical')).toEqual([`${ORIGIN}/fr/16.19.1/changes`]);
    expect(hrefs(head, 'alternate')).toEqual([]);
    expect(head.jsonLd).toEqual([]);
  });
});
