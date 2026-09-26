import type { ProfileBuildCard } from '../../../core/api/generated/models/profile-build-card';
import type { JsonLdNode } from '../../../core/seo/json-ld/core/json-ld-node';
import { breadcrumbList } from '../../../core/seo/json-ld/site/breadcrumb-list';
import { itemList } from '../../../core/seo/json-ld/site/item-list';
import { profilePage } from '../../../core/seo/json-ld/site/profile-page';
import type { SeoUrls } from '../../../core/seo/urls/seo-urls';

/** What the structured data of a public card says, its texts translated. */
export interface ProfileJsonLdFacts {
  readonly name: string;
  readonly description: string;
  /** The skin's splash, root-relative or absolute; null without a skin. */
  readonly image: string | null;
  /** The name of the home, first step of the trail. */
  readonly home: string;
  readonly builds: readonly ProfileBuildCard[];
}

/**
 * The structured data of a public card: the trail from the home, a ProfilePage, and the list
 * of its builds when it has some, which live at `/b/{token}` outside the locales.
 */
export function profileJsonLd(facts: ProfileJsonLdFacts, urls: SeoUrls): JsonLdNode[] {
  const { name, description, image, builds } = facts;
  const nodes = [
    breadcrumbList([
      { name: facts.home, url: urls.page('') },
      { name, url: urls.canonical },
    ]),
    profilePage({
      name,
      url: urls.canonical,
      image: image === null ? null : urls.absolute(image),
      description,
    }),
  ];
  if (builds.length > 0) {
    const links = builds.map((build) => ({
      name: build.name,
      url: urls.absolute(`/b/${build.shareToken}`),
    }));
    nodes.push(itemList(links));
  }
  return nodes;
}
