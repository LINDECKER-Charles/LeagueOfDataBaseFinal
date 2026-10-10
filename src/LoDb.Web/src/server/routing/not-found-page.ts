import SEO_COPY from '../../../public/i18n/seo/en.json';

const NOT_FOUND = SEO_COPY.error['404'];
const ESCAPES: Readonly<Record<string, string>> = {
  '&': '&amp;',
  '<': '&lt;',
  '>': '&gt;',
  '"': '&quot;',
};

function escapeHtml(text: string): string {
  return text.replace(/[&<>"]/g, (character) => ESCAPES[character] ?? character);
}

/**
 * The page of an address outside the grammar of ADR 0005 (`/wp-login.php`, an unknown URL of
 * the former site): a whole document fixed when the server is built, so answering it costs
 * no render. No script at all, as the offline page (`public/offline.html`); English, the
 * language of the pages outside a locale, its copy taken from the `seo` catalogue the
 * rendered 404 reads. Its link goes to `/`, which leads to the visitor's own locale.
 */
export const NOT_FOUND_PAGE = `<!doctype html>
<html lang="en">
  <head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1" />
    <meta name="robots" content="noindex" />
    <title>${escapeHtml(NOT_FOUND.title)} — League Of Data Base</title>
    <style>
      @font-face {
        font-family: 'Beaufort';
        src: url('/fonts/BeaufortforLOL-Bold.woff2') format('woff2');
        font-weight: 700;
        font-display: swap;
      }
      html,
      body {
        height: 100%;
        margin: 0;
      }
      body {
        display: grid;
        place-items: center;
        padding: 2rem;
        box-sizing: border-box;
        background: #0a1428;
        color: #f0e6d2;
        font-family: ui-sans-serif, system-ui, sans-serif;
        text-align: center;
      }
      main {
        max-width: 28rem;
      }
      h1,
      a {
        font-family: 'Beaufort', serif;
        letter-spacing: 0.12em;
        text-transform: uppercase;
      }
      h1 {
        margin: 0;
        font-size: 4.5rem;
        color: #c8aa6e;
      }
      p {
        line-height: 1.6;
        color: #a09b8c;
      }
      a {
        display: inline-block;
        margin-top: 1.2rem;
        padding: 0.8rem 1.6rem;
        border: 1px solid rgba(200, 170, 110, 0.7);
        color: #f0e6d2;
        text-decoration: none;
      }
    </style>
  </head>
  <body>
    <main>
      <p>${escapeHtml(NOT_FOUND.eyebrow)}</p>
      <h1>404</h1>
      <p><strong>${escapeHtml(NOT_FOUND.title)}</strong></p>
      <p>${escapeHtml(NOT_FOUND.body)}</p>
      <a href="/">${escapeHtml(NOT_FOUND.back_home)}</a>
    </main>
  </body>
</html>
`;
