# tools/nginx

Checks of the built nginx image (`docker/nginx/`): the rendered configuration passes
`nginx -t`, nginx runs as its unprivileged user without any capability on a read-only root,
the edge's header is required (`403` without it, `/healthz` aside) and never reaches the
upstreams, and the bounds hold — rates per address with their `429` and `Retry-After`, body
sizes, the paths nginx answers alone, the blobs and their policy, the page caches (one
compressed copy for every client, the past patches' kept from their
`LODB_ARCHIVE_CACHE_MIN_USES`-th request under a key without revision), the bundles' and
their policy, the crawler files' and the former site's caches (under the revision), the blocked
agent refused by its exact string, the AI crawlers let through, the crawler files routed to the
API. The image runs as `compose.yaml` runs it (`--read-only`, `--cap-drop ALL`,
`no-new-privileges`, a tmpfs on `/tmp`, a volume per cache and one for the temporary files,
which a slow client's answer must reach rather than `/tmp`), on its port 8080, behind a stub of
the API and of the SSR server (`lib/upstream-stub.conf`), on a Docker network of its own,
removed afterwards.

```bash
docker build -f docker/nginx/Dockerfile -t lodb-nginx:check .
LODB_NGINX_IMAGE=lodb-nginx:check node --test tools/nginx/test/*.test.mjs
```

Without `LODB_NGINX_IMAGE` the image checks are skipped. `nginx-image.test.mjs` tightens the
rates to a few requests per minute (`LIMITS` in the test) so that each burst is exact; the
bodies keep the image's defaults. Any peer may state the client address there, so that a check
of the caches speaks for a client of its own (`forwardedFor`). The edge's secret is set, as on
a host, and every request sends it unless a check leaves it out.

`nginx-local.test.mjs` boots a second stack as a local one runs it, without a secret: it asks
no header, every location that answers a page sends the constant security headers (an
`add_header` of its own would drop them), `/` answers a remembered locale (`lod_prefs`) with a
`302` never stored and is otherwise never cached, a private page is never kept, and each
alternative of the former site's location reaches the API as sent, an unknown URL the SSR
server's `404`.

The CI's `e2e` job runs both against the image Compose built for the stack, next to
`tools/routing/check-urls.sh`: the Playwright suite leaves these request-level checks to them.

`nginx-maintenance.test.mjs` boots a third stack as a host runs it and turns the maintenance
switch as the deploy job does (`/srv/maintenance/active.json`, a tmpfs there): every page answers
the maintenance page with a `503`, never kept, while the API, the bundles, the probe, the crawler
files and the former site's `301` keep their answers, the page's files and the deploy's state are
served, a request without the edge's header is still refused, and the pages come back once the
switch is gone.

`nginx-noindex.test.mjs` boots a fourth stack as staging runs it (`LODB_NOINDEX=1`): the AI
crawlers (`$lodb_ai_crawler`, `nginx.conf`) get a `403` on both hosts, while a search engine and
a visitor get their answer, each with the `X-Robots-Tag` noindex.

`maintenance-page.test.mjs` and `maintenance-sources.test.mjs` need neither Docker nor the image:
the first runs the page's scripts (`docker/nginx/maintenance/assets`) in a stand-in for the
browser (identity, locale, estimate on the host's clock, reload once the deploy is over), the
second holds the page to the site: the 21 locales and their texts, each identity's tokens and
faces against `src/LoDb.Web/src/styles`, the English a visitor without JavaScript reads.

`compose-environment.test.mjs` needs the Docker CLI only (`docker compose config`): every
`LODB_*` default of the image reaches nginx with the same value through `compose.yaml`, on a
local stack and on a served host, and never as a key without a value, which would drop the
image's default.
