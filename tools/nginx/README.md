# tools/nginx

Checks of the built nginx image (`docker/nginx/`): the rendered configuration passes
`nginx -t`, and the bounds hold — rates per address with their `429` and `Retry-After`,
body sizes, the paths nginx answers alone, the blobs and their policy, the crawler files'
and the former site's caches. The image runs behind a stub of the API and of the SSR server
(`lib/upstream-stub.conf`), on a Docker network of its own, removed afterwards.

```bash
docker build -f docker/nginx/Dockerfile -t lodb-nginx:check .
LODB_NGINX_IMAGE=lodb-nginx:check node --test tools/nginx/test/*.test.mjs
```

Without `LODB_NGINX_IMAGE` the image checks are skipped. They tighten the rates to a few
requests per minute (`LIMITS` in the test) so that each burst is exact; the bodies keep the
image's defaults.

`compose-environment.test.mjs` needs the Docker CLI only (`docker compose config`): every
`LODB_*` default of the image reaches nginx with the same value through `compose.yaml`, on a
local stack and on a served host, and never as a key without a value, which would drop the
image's default.
