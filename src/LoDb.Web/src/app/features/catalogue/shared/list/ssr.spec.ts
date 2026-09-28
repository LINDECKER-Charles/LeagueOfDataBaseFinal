import {
  type ApplicationConfig,
  ChangeDetectionStrategy,
  Component,
  mergeApplicationConfig,
  signal,
} from '@angular/core';
import { type BootstrapContext, bootstrapApplication } from '@angular/platform-browser';
import { provideServerRendering, renderApplication } from '@angular/platform-server';
import { RouterOutlet, provideRouter } from '@angular/router';
import { provideTransloco } from '@jsverse/transloco';
import { of } from 'rxjs';
import type { ItemCard } from '../../../../core/api/generated/models/item-card';
import type { PageContext } from '../../../../core/context/page-context';
import { keepingGlobals } from '../../../../core/testing/keeping-globals';
import { CatalogueLists } from '../data/catalogue-lists';
import type { ListRequest } from '../data/list-request';
import { injectCatalogueList } from '../source/inject-catalogue-list';
import type { CatalogueCardAdapter } from '../state/catalogue-card-adapter';
import { CatalogueCardTemplate } from './catalogue-card-template';
import { CatalogueList } from './catalogue-list';

const CONTEXT: PageContext = { locale: 'en', version: '16.19.1', pinned: false, language: 'en_US' };
const DOCUMENT_HTML =
  '<html><head><base href="/"></head><body><lodb-root></lodb-root></body></html>';
const NAMES = ['Boots', 'Long Sword', 'Dagger', 'Ward', 'Amplifying Tome'];
const ADAPTER: CatalogueCardAdapter<ItemCard> = {
  searchTextOf: (card) => card.name,
  valuesOf: () => ({}),
  keyOf: (card) => card.canonicalPath,
};

@Component({
  selector: 'lodb-items-probe',
  imports: [CatalogueList, CatalogueCardTemplate],
  template: `<lodb-catalogue-list
    [source]="list"
    [adapter]="adapter"
    searchLabel="Search for an item"
    label="Items"
  >
    <p class="card" *lodbCatalogueCard="let card of list">{{ card.name }}</p>
  </lodb-catalogue-list>`,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
class ItemsProbe {
  protected readonly list = injectCatalogueList('items', signal(CONTEXT), 2);
  protected readonly adapter = ADAPTER;
}

// A card holding rich text as Data Dragon writes it: a bare <li>, with no list around it.
@Component({
  selector: 'lodb-rich-probe',
  imports: [CatalogueList, CatalogueCardTemplate],
  template: `<lodb-catalogue-list
    [source]="list"
    [adapter]="adapter"
    searchLabel="Search for an item"
    label="Items"
  >
    <div class="card" *lodbCatalogueCard="let card of list" [innerHTML]="riotText(card)"></div>
  </lodb-catalogue-list>`,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
class RichProbe extends ItemsProbe {
  protected riotText(card: ItemCard): string {
    return `<li>${card.name}</li>`;
  }
}

@Component({
  selector: 'lodb-root',
  imports: [RouterOutlet],
  template: '<router-outlet />',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
class Root {}

function pageOf(request: ListRequest) {
  const { page = 1, size = NAMES.length } = request;
  const names = NAMES.slice((page - 1) * size, page * size);
  const entries = names.map((name) => ({ name, canonicalPath: `items/${name}` }));
  return { kind: 'list', list: { entries, total: NAMES.length }, retryAfterMs: null };
}

async function renderOnServer(url: string, requests: ListRequest[]): Promise<string> {
  const fetch = (_: string, request: ListRequest) => {
    requests.push(request);
    return of(pageOf(request));
  };
  const config: ApplicationConfig = {
    providers: [
      provideRouter([
        { path: 'en/items', component: ItemsProbe },
        { path: 'en/rich', component: RichProbe },
      ]),
      provideTransloco({
        config: { defaultLang: 'en', missingHandler: { logMissingKey: false }, prodMode: true },
        loader: class {
          getTranslation = () => of({});
        },
      }),
      { provide: CatalogueLists, useValue: { fetch } },
    ],
  };
  Reflect.set(globalThis, 'ngServerMode', true);
  try {
    const server = mergeApplicationConfig(config, { providers: [provideServerRendering()] });
    const bootstrap = (context: BootstrapContext) => bootstrapApplication(Root, server, context);
    return await keepingGlobals(() =>
      renderApplication(bootstrap, { document: DOCUMENT_HTML, url }),
    );
  } finally {
    Reflect.set(globalThis, 'ngServerMode', undefined);
  }
}

describe('lodb-catalogue-list rendered on the server', () => {
  it('renders the page the URL names, readable as is, and links the next', async () => {
    const requests: ListRequest[] = [];
    const html = await renderOnServer('/en/items?page=2', requests);
    expect(requests).toEqual([{ version: '16.19.1', lang: 'en_US', page: 2, size: 2 }]);
    const cards = [...html.matchAll(/<p class="card">\s*([^<]+?)\s*<\/p>/g)].map((m) => m[1]);
    expect(cards).toEqual(['Dagger', 'Ward']);
    expect(html).toContain('href="/en/items" rel="prev"');
    expect(html).toContain('href="/en/items?page=3"');
  });

  it('keeps a card whose rich text holds a bare <li> inside its cell once parsed', async () => {
    const html = await renderOnServer('/en/rich', []);
    // The browser parses the server's HTML as a whole document, as hydration then finds it.
    const parsed = new DOMParser().parseFromString(html, 'text/html');
    const cells = [...parsed.querySelectorAll('lodb-catalogue-list .grid__cell')];
    expect(cells.map((cell) => cell.querySelector('.card li')?.textContent)).toEqual([
      'Boots',
      'Long Sword',
    ]);
  });
});
