import type { AudienceData } from '../audience-data';
import type { AudienceDevice } from '../audience-device';
import type { AudienceHit } from '../audience-hit';
import { AUDIENCE_SITE } from '../audience-site';
import type { UmamiSendBody } from './umami-send-body';

function dataOf(hit: AudienceHit): AudienceData {
  const { locale, version } = hit.page;
  return { ...(locale === null ? {} : { locale }), version, ...hit.data };
}

/**
 * The `send` body of a hit: the platform as the tag, the locale and the version of the page
 * as data, the site's own hostname whatever the host the browser shows.
 */
export function umamiBodyOf(hit: AudienceHit, device: AudienceDevice): UmamiSendBody {
  return {
    type: 'event',
    payload: {
      website: AUDIENCE_SITE.websiteId,
      hostname: AUDIENCE_SITE.hostname,
      url: hit.page.url,
      referrer: hit.referrer,
      title: hit.title,
      language: device.language,
      screen: device.screen,
      tag: device.platform,
      ...(hit.name === undefined ? {} : { name: hit.name }),
      data: dataOf(hit),
    },
  };
}
