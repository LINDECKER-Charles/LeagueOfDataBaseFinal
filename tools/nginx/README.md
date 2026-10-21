# tools/nginx

Checks of the built nginx image (`docker/nginx/`): the rendered configuration passes
`nginx -t`, nginx runs as its unprivileged user without any capability on a read-only root,
the edge's header is required (`403` without it, `/healthz` aside) and never reaches the
upstreams, nor does a client's prerender marker (`X-LoDb-Prerender`), and the bounds hold —
rates per address with their `429` and `Retry-After`, body sizes, the paths nginx answers
alone, the blobs and their policy, the page caches (one compressed copy for every client, the
past patches' kept from their `LODB_ARCHIVE_CACHE_MIN_USES`-th request under a key of both
epochs, without revision), the bundles' and their policy, the crawler files' and the former
site's caches (under the revision), the blocked agent refused by its exact string, the AI
crawlers let through, the crawler files routed to the API. The image runs as `compose.yaml`
runs it (`--read-only`, `--cap-drop ALL`, `no-new-privileges`, a tmpfs on `/tmp`, a volume per
cache and one for the temporary files, which a slow client's answer must reach rather than
`/tmp`), on its port 8080, behind a stub of the API, of the SSR server and of the page store
(`lib/upstream-stub.conf`), on a Docker network of its own, removed afterwards.

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

`nginx-pages-store.test.mjs` boots the image in front of the stub's page store (lot 3 of
ADR 0011): an S3 bucket read in path style on the alias `lodb-pages-store`, over plain HTTP on
`9000` and over TLS on `9443`, under a certificate no authority signed
(`lib/self-signed-certificate.mjs`, the image has no `openssl`). The stub keeps a page
compressed with its headers in metadata, answers `404` for a key it does not hold, and one
version stands for each of its other answers (`403`, `429`, `500`, `503`, a listener that never
answers); `storeRequests()` (`lib/stack.mjs`) reads what reached it. A stored page comes from the
store then from the cache (`X-LoDb-Source`: `store`, `cache`), with its policy, report-only
policy, reporting endpoints and `Link` posed again from the metadata (an object without its
policy gets a strict one, and any stored page the type of a page), the `archived` class's
one-year `Cache-Control`, none of the store's own headers (`x-amz-*`, `x-amzn-*`, CORS, as the
local stack's simulated store sends them, nor the validators and ranges S3 states of the
compressed object: a `HEAD` gets the headers of a `GET`, a conditional request on the store's
`ETag` or `Last-Modified` and a `Range` get the whole page), and decompressed for a client
without gzip; the store gets the key with its own `Host` and none of the client's headers; each
of its failures falls back to the SSR server (`ssr`) within the store's timeouts; a query,
another method or a path no pass writes never reaches it, nor the latest patch pinned in the
path (the stub API's `/api/meta` states `15.10.1`, read by `njs/latest-version.js`), whose
redirect the SSR server answers, while a patch that only starts like it still asks the store;
a client's rate holds on both ways, with the `429` and `Retry-After` of every page, while the
store's own `429` falls back. A bundle the SSR server no longer has (its `404`, and only that)
comes from the store's `build/`, then from the bundles' cache under its name, without the
store's own headers; a name neither has is a plain `404`, whatever the store answered, and past
the pages' rate a client gets their `429` without the store being asked. Switched off
(`LODB_PAGES_STORE_ENABLED=0`, the image's default), nginx never asks the store.
`nginx-pages-store-unreachable.test.mjs` boots it twice more, in front of a store whose
certificate it cannot verify, then one whose name does not resolve: each time it starts and
renders every page with the SSR server.

`maintenance-page.test.mjs` and `maintenance-sources.test.mjs` need neither Docker nor the image:
the first runs the page's scripts (`docker/nginx/maintenance/assets`) in a stand-in for the
browser (identity, locale, estimate on the host's clock, reload once the deploy is over), the
second holds the page to the site: the 21 locales and their texts, each identity's tokens and
faces against `src/LoDb.Web/src/styles`, the English a visitor without JavaScript reads.

`archived-cache-control.test.mjs` needs neither: the `Cache-Control` nginx gives a page from the
store (`sites/site.conf`) is the SSR server's `archived` class
(`src/LoDb.Web/src/app/core/routing/response/cache-control.ts`), the same constant twice.

`compose-environment.test.mjs` needs the Docker CLI only (`docker compose config`): every
`LODB_*` default of the image reaches nginx with the same value through `compose.yaml`, on a
local stack and on a served host, and never as a key without a value, which would drop the
image's default.

What no check here can show is a read of the real store: the CI's store is a stub over plain
HTTP (and a certificate no authority signed, which must fail), the end-to-end suite's a
simulated one. Before lot 3 serves a host, once a pass has written a page to that host's
bucket, ask that page through the host's nginx and expect `X-LoDb-Source: store` on a `MISS`:
`ssr` there means that nginx cannot read the store (certificate chain beyond
`proxy_ssl_verify_depth`, authorities of the image, the bucket's policy or the host's address),
and its error log names the cause. The deploy that brings lot 3 starts the past patches' cache
again from cold (its key gains the served epoch) while the store is still filling: deploy it at
a quiet hour and expect the pinned pages' `503` to rise for a while, as the SSR server renders
them again.
