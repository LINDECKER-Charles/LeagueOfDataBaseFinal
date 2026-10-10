# server.d

Every `*.conf` file of this folder is included inside the site's `server` block
(`sites/site.conf`), after its own locations. The image copies the whole folder, so a
new file needs no change to the Dockerfile. Like `sites/` and `snippets/`, every file is a
template: `${LODB_…}` is replaced at start (Dockerfile), nginx's own `$variables` are not.

Reserved files (plan, §7.3):

| File | Chantier |
|---|---|
| `seo.conf` | L3.4 (sitemaps, robots.txt, llms.txt routed to the API, cached) |
| `legacy-redirects.conf` | L3.12 (301 of the old URLs, cached) |
| `analytics.conf` | L7.1 (internal target of the page-view mirror) |

What goes inside a `location` belongs in `../snippets/` instead. The guards both server
blocks share (refused agents, client timeouts, paths that exist nowhere, the 429 answer)
live in `../snippets/server-guards.conf`, which the API subdomain includes too.

Files of the served environment, not of the image: the environment mounts a folder
read-only on `/etc/nginx/android/` (a folder rather than each file, so that a replaced
file reaches nginx). Without them, their URL answers 404.

| File in `/etc/nginx/android/` | Served on | Conf | Source |
|---|---|---|---|
| `assetlinks.json` | `/.well-known/assetlinks.json` | `assetlinks.conf` | `tools/android/assetlinks.mjs`, with the fingerprints of the app's signing certificates |
| `latest.json` | `/android/latest.json`, CORS for `https://localhost` | `android-latest.conf` | asset `lodb-android-latest.json` of each Android release, copied at every publication while the transitional channel is on |
