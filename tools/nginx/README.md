# tools/nginx

Checks of the built nginx image (`docker/nginx/`): the rendered configuration passes
`nginx -t`, nginx runs as its unprivileged user without any capability on a read-only root,
and the bounds hold — rates per address with their `429` and `Retry-After`, body
sizes, the paths nginx answers alone, the blobs and their policy, the page caches (one
compressed copy for every client, the past patches' kept from their
`LODB_ARCHIVE_CACHE_MIN_USES`-th request under a key without revision), the bundles' and
their policy, the crawler files' and the former site's caches (under the revision). The image
runs as `compose.yaml` runs it (`--read-only`, `--cap-drop ALL`, `no-new-privileges`, a tmpfs
on `/tmp`, a volume per cache), on its port 8080, behind a stub of the API and of the SSR
server (`lib/upstream-stub.conf`), on a Docker network of its own, removed afterwards.

```bash
docker build -f docker/nginx/Dockerfile -t lodb-nginx:check .
LODB_NGINX_IMAGE=lodb-nginx:check node --test tools/nginx/test/*.test.mjs
```

Without `LODB_NGINX_IMAGE` the image checks are skipped. They tighten the rates to a few
requests per minute (`LIMITS` in the test) so that each burst is exact; the bodies keep the
image's defaults. Any peer may state the client address there, so that a check of the caches
speaks for a client of its own (`forwardedFor`).

`compose-environment.test.mjs` needs the Docker CLI only (`docker compose config`): every
`LODB_*` default of the image reaches nginx with the same value through `compose.yaml`, on a
local stack and on a served host, and never as a key without a value, which would drop the
image's default.
