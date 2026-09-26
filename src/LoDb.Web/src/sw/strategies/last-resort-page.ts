// Only reached when even the offline page is missing from the cache: still a page, never a
// blank one, and inline so that it needs nothing from the network.
const BODY =
  '<!doctype html><html lang="en"><meta charset="utf-8">' +
  '<meta name="viewport" content="width=device-width, initial-scale=1">' +
  '<title>Offline — League Of Data Base</title>' +
  '<p lang="en">You are offline. This page is not available yet.</p>' +
  '<p lang="fr">Vous êtes hors ligne. Cette page n’est pas encore disponible.</p></html>';

/** The page answered when neither the network, nor a copy, nor the offline page is there. */
export function lastResortPage(): Response {
  return new Response(BODY, {
    status: 503,
    headers: { 'Content-Type': 'text/html; charset=utf-8', 'Cache-Control': 'no-store' },
  });
}
