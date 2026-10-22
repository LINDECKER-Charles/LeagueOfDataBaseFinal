# server.d

Every `*.conf` file of this folder is included inside the site's `server` block
(`sites/site.conf`), after its own locations. The image copies the whole folder, so a
new file needs no change to the Dockerfile. Like `sites/` and `snippets/`, every file is a
template: `${LODB_…}` is replaced at start (Dockerfile), nginx's own `$variables` are not.

| File | Role |
|---|---|
| `seo.conf` | crawler files built by the API (`/sitemap.xml`, `/sitemaps/`, `/robots.txt`, `/llms.txt`, the IndexNow key file `/{key}.txt`), cached (`../snippets/seo-cache.conf`) |
| `legacy-redirects.conf` | the former site's URLs: one 301 to their new form, resolved by the API (`/api/legacy`), cached |
| `analytics.conf` | `/_analytics/page`, internal target of the page-view mirror (`../snippets/analytics-mirror.conf`), forwarded to the API |
| `maintenance.conf` | maintenance page of the deploys (the page itself: `../maintenance/`) |
| `assetlinks.conf`, `android-latest.conf` | files of the served environment for the Android app (below) |

What goes inside a `location` belongs in `../snippets/` instead. The guards both server
blocks share (refused agents, client timeouts, paths that exist nowhere, the 429 answer)
live in `../snippets/server-guards.conf`, which the API subdomain includes too. Every server
block, the redirect of `www.` and `.fr` included, starts with `../snippets/edge-guard.conf`:
a request without the edge's header is refused there, before anything else.

Files of the served environment, not of the image: the environment mounts a folder
read-only on `/etc/nginx/android/` (a folder rather than each file, so that a replaced
file reaches nginx). Without them, their URL answers 404. The same goes for
`/srv/maintenance/`, the deploy job's maintenance switch (`active.json`,
`tools/deploy/maintenance.sh`): while the file is there, every page answers the maintenance
page, and `/maintenance/status.json` serves it to that page; without it, a 404.

| File in `/etc/nginx/android/` | Served on | Conf | Source |
|---|---|---|---|
| `assetlinks.json` | `/.well-known/assetlinks.json` | `assetlinks.conf` | `tools/android/assetlinks.mjs`, with the fingerprints of the app's signing certificates |
| `latest.json` | `/android/latest.json`, CORS for `https://localhost` | `android-latest.conf` | asset `lodb-android-latest.json` of each Android release, copied at every publication while the transitional channel is on |
