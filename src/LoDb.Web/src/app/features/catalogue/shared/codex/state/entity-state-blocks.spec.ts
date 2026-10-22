import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ChangeDetectionStrategy, Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import type { CanonicalLink } from '../../../../../core/api/generated/models/canonical-link';
import type { EntityState } from '../../../../../core/api/generated/models/entity-state';
import type { PageContext } from '../../../../../core/context/page-context';
import { detailStateOf } from '../../../../../core/testing/detail-state-of';
import { provideTestTransloco } from '../../../../../core/testing/provide-test-transloco';
import { ROOT_CATALOGUES } from '../../../../../core/testing/root-catalogues';
import { EntityStateBlocks } from './entity-state-blocks';
import type { StatefulDetails } from './stateful-details';

const LATEST = '16.20.1';
const PATH = 'items/3078-trinity-force';
const VERSIONED = /^\/en\/\d+(\.\d+)+\//;

let context: PageContext;
let details: StatefulDetails;

@Component({
  selector: 'lodb-host',
  imports: [EntityStateBlocks],
  template: `<lodb-entity-state [context]="context" [details]="details" />`,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
class Host {
  protected readonly context = context;
  protected readonly details = details;
}

function at(version: string | null, path = PATH): CanonicalLink {
  return { version, path };
}

// The state opened at `opener`, closed at `last` (open when null), seen from `version`.
function stateOf(opener: string, last: string | null, version: string): EntityState {
  return {
    openerVersion: opener,
    lastVersion: last,
    isOpen: last === null,
    isOpener: version === opener,
    kind: 'changed',
    changedFields: [{ field: 'goldTotal', subject: null }],
    openerLink: at(opener === LATEST ? null : opener),
    canonical: last === null ? at(null) : at(opener),
  };
}

// The block of the page of `version` at `url`, the API's answer being `fields`.
async function render(
  version: string,
  fields: Partial<StatefulDetails>,
  url = `/en/${version}/${PATH}`,
): Promise<HTMLElement> {
  context = { locale: 'en', version, pinned: version !== LATEST, language: 'en_US' };
  details = { ...detailStateOf(PATH), canonicalPath: PATH, isPresentInLatest: false, ...fields };
  TestBed.configureTestingModule({
    providers: [
      provideHttpClient(),
      provideHttpClientTesting(),
      provideRouter([{ path: '**', component: Host }]),
      provideTestTransloco({ otherwise: ROOT_CATALOGUES.en }),
    ],
  });
  const harness = await RouterTestingHarness.create(url);
  await harness.fixture.whenStable();
  return harness.routeNativeElement as HTMLElement;
}

function linksOf(block: HTMLElement): { text: string; href: string | null }[] {
  return Array.from(block.querySelectorAll('a'), (link) => ({
    text: link.textContent?.trim() ?? '',
    href: link.getAttribute('href'),
  }));
}

function textOf(element: Element | null | undefined): string {
  return element?.textContent?.replace(/\s+/g, ' ').trim() ?? '';
}

// The history of Trinity Force up to its state opened at 11.5.1 and closed at 11.9.1.
const FROZEN_HISTORY: StatefulDetails['history'] = [
  {
    openerVersion: '11.5.1',
    lastVersion: '11.9.1',
    kind: 'changed',
    changedFields: [{ field: 'goldTotal', subject: null }],
    link: at('11.5.1'),
  },
  {
    openerVersion: '10.1.1',
    lastVersion: '10.1.1',
    kind: 'new',
    changedFields: [],
    link: at('10.1.1'),
  },
];

describe('EntityStateBlocks', () => {
  // The blocks read the API's answer alone: no request, no `/api/meta`.
  afterEach(() => TestBed.inject(HttpTestingController).verify());

  it('links only versioned URLs from a frozen page, but the latest patch', async () => {
    const block = await render('11.5.1', {
      state: stateOf('11.5.1', '11.9.1', '11.5.1'),
      history: FROZEN_HISTORY,
      next: { kind: 'changed', version: '11.10.1', link: at('11.10.1') },
      isPresentInLatest: true,
      changes: { kind: 'changed', comparedVersion: '10.1.1', fields: [] },
    });
    const links = linksOf(block);

    expect(links.filter((link) => !VERSIONED.test(link.href ?? ''))).toEqual([
      { text: 'In the latest patch →', href: `/en/${PATH}` },
    ]);
    expect(links.map((link) => link.text)).toEqual([
      'In the latest patch →',
      'Changed in patch 11.10.1',
      'Patches 11.5.1 to 11.9.1',
      'Patch 10.1.1',
    ]);
  });

  it('writes a text diff as text, struck out and inserted, never as HTML', async () => {
    const block = await render('11.5.1', {
      state: stateOf('11.5.1', '11.9.1', '11.5.1'),
      changes: {
        kind: 'changed',
        comparedVersion: '10.1.1',
        fields: [
          {
            field: 'description',
            subject: null,
            kind: 'text',
            valueKind: 'text',
            before: [],
            after: [],
            segments: [
              { kind: 'same', text: 'Deals ' },
              { kind: 'removed', text: '<b>40</b>' },
              { kind: 'added', text: '<img src=x onerror=alert(1)>60' },
              { kind: 'same', text: ' damage.' },
            ],
          },
        ],
      },
    });

    expect(textOf(block.querySelector('h2'))).toBe('What changed in this patch');
    expect(textOf(block.querySelector('section p'))).toBe('Compared with patch 10.1.1.');
    expect(textOf(block.querySelector('dt'))).toBe('Description');
    expect(block.querySelector('del')?.textContent).toBe('<b>40</b>');
    expect(block.querySelector('ins')?.textContent).toBe('<img src=x onerror=alert(1)>60');
    expect(block.querySelector('dd b, dd img')).toBeNull();
  });

  // Crawlers and screen readers read the text: no two values may run into one word.
  it('keeps the values of a row apart in its text, the arrow between them read', async () => {
    const block = await render('11.5.1', {
      state: stateOf('11.5.1', '11.9.1', '11.5.1'),
      changes: {
        kind: 'changed',
        comparedVersion: '10.1.1',
        fields: [
          {
            field: 'skins',
            subject: null,
            kind: 'set',
            valueKind: 'entity',
            before: [{ value: '103001', label: 'Dynasty Ahri' }],
            after: [
              { value: '103002', label: 'Midnight Ahri' },
              { value: '103003', label: 'Foxfire Ahri' },
            ],
            segments: [],
          },
          {
            field: 'goldTotal',
            subject: null,
            kind: 'value',
            valueKind: 'number',
            before: [{ value: '3000', label: null }],
            after: [{ value: '3100', label: null }],
            segments: [],
          },
        ],
      },
    });
    const texts = Array.from(block.querySelectorAll('dd'), (row) => row.textContent?.trim());

    expect(texts).toEqual(['Dynasty Ahri Midnight Ahri, Foxfire Ahri', '3,000 → 3,100']);
    expect(block.querySelector('dd [aria-hidden]')).toBeNull();
  });

  it('says since when a page of a state it did not open is unchanged, linking its opener', async () => {
    const block = await render('11.7.1', {
      state: stateOf('11.5.1', '11.9.1', '11.7.1'),
      history: FROZEN_HISTORY,
      next: { kind: 'removed', version: '11.10.1', link: null },
    });

    expect(linksOf(block)[0]).toEqual({
      text: 'Unchanged since patch 11.5.1',
      href: `/en/11.5.1/${PATH}`,
    });
    expect(textOf(block.querySelector('ol li'))).toBe('Removed in patch 11.10.1');
    expect(block.querySelector('ol li:first-child a')).toBeNull();
    expect(linksOf(block).every((link) => VERSIONED.test(link.href ?? ''))).toBe(true);
  });

  // A revisable page: what it shows changes only when its own state closes.
  it('names no later patch on a page of the open state', async () => {
    const block = await render('16.18.1', {
      state: stateOf('16.17.1', null, '16.18.1'),
      history: [
        {
          openerVersion: '16.17.1',
          lastVersion: null,
          kind: 'returned',
          changedFields: [],
          link: at(null),
        },
      ],
      isPresentInLatest: true,
    });

    const row = block.querySelector('ol li');
    expect(Array.from(row?.children ?? [], (part) => textOf(part))).toEqual([
      'Since patch 16.17.1',
      'Back',
    ]);
    expect(block.textContent).not.toContain(LATEST);
    expect(linksOf(block).map((link) => link.href)).toEqual([
      `/en/${PATH}`,
      `/en/16.17.1/${PATH}`,
      `/en/${PATH}`,
    ]);
  });

  it('links the opener of the open state from the short URL, the regional variant kept', async () => {
    const block = await render(
      LATEST,
      { state: stateOf('16.17.1', null, LATEST), isPresentInLatest: true },
      `/en/${PATH}?lang=en_GB`,
    );

    expect(linksOf(block)).toEqual([
      { text: 'Unchanged since patch 16.17.1', href: `/en/16.17.1/${PATH}?lang=en_GB` },
    ]);
  });

  // The coverage starts at 10.1.1: nothing before it was compared.
  it('tells no change and no chip for a state opened at the start of the index', async () => {
    const block = await render('10.1.1', {
      state: { ...stateOf('10.1.1', '10.1.1', '10.1.1'), kind: 'baseline', changedFields: [] },
      history: [{ ...FROZEN_HISTORY[1]!, kind: 'baseline' }],
      next: { kind: 'changed', version: '11.5.1', link: at('11.5.1') },
      changes: { kind: 'baseline', comparedVersion: null, fields: [] },
    });

    expect(Array.from(block.querySelectorAll('h2'), (title) => textOf(title))).toEqual(['History']);
    expect(block.querySelector('.hx-chip-hex')).toBeNull();
    expect(Array.from(block.querySelectorAll('ol li'), (row) => textOf(row))).toEqual([
      'Changed in patch 11.5.1',
      'Patch 10.1.1',
    ]);
  });

  it('says nothing of a page outside the index but where its entity is now', async () => {
    const block = await render('3.6.14', { isPresentInLatest: true });

    expect(linksOf(block)).toEqual([{ text: 'In the latest patch →', href: `/en/${PATH}` }]);
    expect(block.querySelector('h2')).toBeNull();
  });
});
