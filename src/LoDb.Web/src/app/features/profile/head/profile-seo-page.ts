import type { PublicProfile } from '../../../core/api/generated/models/public-profile';
import type { Locale } from '../../../core/i18n/locales';
import type { SeoPage } from '../../../core/seo/seo-page';
import { displayName } from '../shared/display-name';
import { profileJsonLd } from './profile-json-ld';

/** A root translation in the page's locale. */
export type ProfileTranslate = (key: string, params?: Record<string, string>) => string;

/**
 * The head of a public card: the summoner's name as title, the favorite skin's splash as the
 * image of its link previews, and its structured data. Indexable: a private card never gets
 * here, the resolver answers its 404.
 */
export function profileSeoPage(
  profile: PublicProfile,
  translate: ProfileTranslate,
  locale: Locale,
): SeoPage {
  const name = displayName(profile.username, profile.riotTagline);
  const description = translate('profile.public.description', { username: name });
  const image = profile.showcase.skin?.splash ?? null;
  const home = translate('header.navigation.home');
  return {
    title: name,
    description,
    locale,
    image,
    ogType: 'profile',
    path: `u/${encodeURIComponent(profile.username)}`,
    jsonLd: (urls) =>
      profileJsonLd({ name, description, image, home, builds: profile.builds }, urls),
  };
}
