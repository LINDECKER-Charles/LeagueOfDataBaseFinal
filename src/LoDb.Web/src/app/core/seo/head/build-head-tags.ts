import { previewImage } from '../images/preview-image';
import type { SeoImage } from '../images/seo-image';
import { siteGraph } from '../json-ld/site/site-graph';
import type { SeoPage } from '../seo-page';
import { SITE_IDENTITY } from '../site-identity';
import { alternatesOf } from '../urls/alternates-of';
import { hreflangOf } from '../urls/hreflang-of';
import type { PageAddress } from '../urls/page-address';
import type { SeoUrls } from '../urls/seo-urls';
import { seoUrlsOf } from '../urls/seo-urls-of';
import type { HeadInput } from './head-input';
import type { HeadTags } from './head-tags';
import { ogLocaleOf } from './og-locale-of';
import { pageTitle } from './page-title';

type PageKind = NonNullable<SeoPage['kind']>;
type CanonicalTarget = Extract<SeoPage, { readonly path: string }>['canonical'];
type Metas = HeadTags['metas'];

const ROBOTS: Record<PageKind, string> = {
  page: 'index, follow',
  // Out of the index, but its links still lead crawlers on.
  noindex: 'noindex, follow',
  private: 'noindex, nofollow',
  error: 'noindex, nofollow',
  share: 'noindex, nofollow',
};
const TWITTER_CARD = 'summary_large_image';
const SITEMAP = { rel: 'sitemap', href: '/sitemap.xml', type: 'application/xml', title: 'Sitemap' };

/**
 * The head of a page. Only an addressed page gets a canonical, and only an indexable one that
 * is its own canonical the site graph and its 21 alternates: an alternate pointing at a page
 * canonical elsewhere or `noindex` would send crawlers mixed signals, and the structured data
 * of a past patch would contradict, under the same ids, those of its canonical.
 */
export function buildHeadTags(input: HeadInput): HeadTags {
  const title = pageTitle(input.page, input.texts);
  const { urls, robots, links, isIndexedHere } = indexingOf(input);
  return {
    title,
    metas: [...pageMetas(input, title, urls), ...siteMetas(robots)],
    links: [...links, SITEMAP],
    jsonLd: isIndexedHere && urls !== null ? jsonLdOf(input, title, urls) : [],
  };
}

/** How crawlers may index a page, and under which URL. */
interface Indexing {
  /** The URLs of the page's canonical; null when it has none. */
  readonly urls: SeoUrls | null;
  readonly robots: string;
  /** The canonical, then the alternates of an indexable page that is its own canonical. */
  readonly links: HeadTags['links'];
  /** Whether the page is indexable and its own canonical: the one indexed under its URL. */
  readonly isIndexedHere: boolean;
}

function indexingOf(input: HeadInput): Indexing {
  const { page, origin, locale } = input;
  const kind: PageKind = page.kind ?? 'page';
  if (!('path' in page)) {
    return { urls: null, robots: ROBOTS[kind], links: [], isIndexedHere: false };
  }
  const address: PageAddress = { origin, locale, version: page.version ?? null, path: page.path };
  const canonical = canonicalOf(address, page.canonical);
  const urls = seoUrlsOf(canonical);
  const isIndexedHere = kind === 'page' && isSamePage(canonical, address);
  const alternates = isIndexedHere ? alternateLinks(address) : [];
  return {
    urls,
    robots: ROBOTS[kind],
    links: [{ rel: 'canonical', href: urls.canonical }, ...alternates],
    isIndexedHere,
  };
}

// A page of the latest version is its own canonical. A pinned one is indexed where the API
// places it (an entity's state), by default as the same page at the latest version.
function canonicalOf(address: PageAddress, target: CanonicalTarget): PageAddress {
  if (address.version === null || target === undefined) {
    return { ...address, version: null };
  }
  return { ...address, version: target.version, path: target.path };
}

function isSamePage(page: PageAddress, other: PageAddress): boolean {
  return page.version === other.version && page.path === other.path;
}

function pageMetas(input: HeadInput, title: string, urls: SeoUrls | null): Metas {
  const { page, origin, locale } = input;
  const description = page.description ?? input.texts.siteDescription;
  const home = seoUrlsOf({ origin, locale, version: null, path: '' });
  const image = imageOf(page.image);
  return [
    { attribute: 'name', key: 'description', content: description },
    { attribute: 'property', key: 'og:title', content: title },
    { attribute: 'property', key: 'og:description', content: description },
    ...(urls === null ? [] : [ogUrl(urls)]),
    { attribute: 'property', key: 'og:type', content: page.ogType ?? 'website' },
    { attribute: 'property', key: 'og:locale', content: ogLocaleOf(locale) },
    { attribute: 'name', key: 'twitter:title', content: title },
    { attribute: 'name', key: 'twitter:description', content: description },
    ...imageMetas({ ...image, url: home.absolute(image.url), alt: image.alt ?? title }),
  ];
}

function imageOf(image: SeoPage['image']): SeoImage {
  if (image === undefined || image === null) {
    return previewImage('home');
  }
  return typeof image === 'string' ? { url: image } : image;
}

// The image, absolute, with its size when known and what it shows.
function imageMetas(image: SeoImage & { readonly alt: string }): Metas {
  const size: Metas =
    image.width === undefined || image.height === undefined
      ? []
      : [
          { attribute: 'property', key: 'og:image:width', content: String(image.width) },
          { attribute: 'property', key: 'og:image:height', content: String(image.height) },
        ];
  return [
    { attribute: 'property', key: 'og:image', content: image.url },
    ...size,
    { attribute: 'property', key: 'og:image:alt', content: image.alt },
    { attribute: 'name', key: 'twitter:image', content: image.url },
    { attribute: 'name', key: 'twitter:image:alt', content: image.alt },
  ];
}

function ogUrl(urls: SeoUrls): Metas[number] {
  return { attribute: 'property', key: 'og:url', content: urls.canonical };
}

function siteMetas(robots: string): Metas {
  return [
    { attribute: 'name', key: 'robots', content: robots },
    { attribute: 'property', key: 'og:site_name', content: SITE_IDENTITY.name },
    { attribute: 'name', key: 'twitter:card', content: TWITTER_CARD },
    ...SITE_IDENTITY.googleSiteVerifications.map((token) => ({
      attribute: 'name' as const,
      key: 'google-site-verification',
      content: token,
    })),
  ];
}

function alternateLinks(address: PageAddress): HeadTags['links'] {
  return alternatesOf(address).map((alternate) => ({ rel: 'alternate', ...alternate }));
}

function jsonLdOf(input: HeadInput, title: string, urls: SeoUrls): HeadTags['jsonLd'] {
  const own = 'jsonLd' in input.page ? (input.page.jsonLd?.(urls) ?? []) : [];
  const graph = siteGraph({
    urls,
    pageName: title,
    siteDescription: input.texts.siteDescription,
    inLanguage: hreflangOf(input.locale),
  });
  return [graph, ...own];
}
