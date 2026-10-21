// The latest patch as the API states it (GET /api/meta, its `latest`), kept in a shared zone that
// every worker reads (sites/site.conf). No pass ever stores the latest patch's pages: its pinned
// URLs (/en/16.21.1/…) go straight to the SSR server, which answers their 301 to the short URL,
// rather than through a miss of the page store first ($lodb_latest_pinned, sites/site.conf).
// The latest patch changes with a promotion, between deploys: nginx cannot learn it at start.

// The API on the stack's network, by its project alias (nginx.conf), never through the site:
// nothing of the edge's nor of a client's applies to this read.
const META_URL = 'http://lodb-api:8080/api/meta';
// /api/meta lists the versions twice and the languages, a few kilobytes: far below this bound,
// above njs's default of 32 KiB.
const META_MAX_BYTES = 262144;
// A Data Dragon version as the path grammar writes it (src/LoDb.Domain/Versions); anything else
// is never kept, so nothing but a version reaches the map that reads it.
const VERSION = /^[0-9]+(?:\.[0-9]+)+$/;
const LATEST_KEY = 'latest';

// Run on one worker every interval (js_periodic, snippets/pages-store/pages.conf). An answer
// that fails or states no version keeps the last one read: a promotion is the API's own doing,
// so a stale value lasts no longer than the API's silence. With none read yet, the variable is
// empty and nginx asks the store as it did before this read existed.
async function refresh() {
  try {
    const reply = await ngx.fetch(META_URL, { max_response_body_size: META_MAX_BYTES });
    if (!reply.ok) {
      return;
    }
    const meta = await reply.json();
    if (typeof meta.latest === 'string' && VERSION.test(meta.latest)) {
      ngx.shared.lodb_latest_version.set(LATEST_KEY, meta.latest);
    }
  } catch (error) {
    // The API restarting or unreachable: the next interval tries again.
  }
}

// $lodb_latest_version (js_set, sites/site.conf): the version read last, or "".
function read() {
  return ngx.shared.lodb_latest_version.get(LATEST_KEY) || '';
}

export default { refresh, read };
