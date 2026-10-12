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
type Metas = HeadTags['metas'];

const ROBOTS: Record<PageKind, string> = {
  page: 'index, follow',
  // The return pages of a donation: out of the index, but their links still count.
  'donation-return': 'noindex, follow',
  private: 'noindex, nofollow',
  error: 'noindex, nofollow',
  share: 'noindex, nofollow',
};
const TWITTER_CARD = 'summary_large_image';
const SITEMAP = { rel: 'sitemap', href: '/sitemap.xml', type: 'application/xml', title: 'Sitemap' };

/**
 * The head of a page. Only an addressed page gets a canonical and the site graph, and only
 * an indexable one its 21 alternates: a canonical or an alternate pointing at a `noindex`
 * page would send crawlers mixed signals.
 */
export function buildHeadTags(input: HeadInput): HeadTags {
  const { page, origin, locale } = input;
  const title = pageTitle(page, input.texts);
  const address: PageAddress | null =
    'path' in page ? { origin, locale, version: page.version ?? null, path: page.path } : null;
  const urls = address === null ? null : seoUrlsOf(address);
  const kind: PageKind = page.kind ?? 'page';
  return {
    title,
    metas: [...pageMetas(input, title, urls), ...siteMetas(kind)],
    links: [
      ...(urls === null ? [] : [{ rel: 'canonical', href: urls.canonical }]),
      ...(address !== null && kind === 'page' ? alternateLinks(address) : []),
      SITEMAP,
    ],
    jsonLd: urls === null ? [] : jsonLdOf(input, title, urls),
  };
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

function siteMetas(kind: PageKind): Metas {
  return [
    { attribute: 'name', key: 'robots', content: ROBOTS[kind] },
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
