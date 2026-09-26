# server.d

Every `*.conf` file of this folder is included inside the site's `server` block
(`sites/site.conf`), after its own locations. The image copies the whole folder, so a
new file needs no change to the Dockerfile. Files here are plain nginx, not templates.

Reserved files (plan, §7.3):

| File | Chantier |
|---|---|
| `seo.conf` | L3.4 (sitemaps, robots.txt, llms.txt routed to the API) |
| `legacy-redirects.conf` | L3.12 (301 of the old URLs) |
| `analytics.conf` | L7.1 (internal target of the page-view mirror) |

What goes inside a `location` belongs in `../snippets/` instead.
