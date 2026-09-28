// What makes the site installable. The server render leaves them out: they only matter to a
// browser that runs the page, and the shell build, embedded by the apps, has no use for them.
const LINKS: readonly (readonly [rel: string, href: string])[] = [
  ['manifest', '/manifest.webmanifest'],
  ['apple-touch-icon', '/pwa/apple-touch-icon.png'],
];
const THEME_COLOR = '#010a13';

/** Adds the web app manifest, the Apple touch icon and the theme colour to the head, once. */
export function appendInstallLinks(document: Document): void {
  for (const [rel, href] of LINKS) {
    if (document.head.querySelector(`link[rel="${rel}"]`) === null) {
      const link = document.createElement('link');
      link.rel = rel;
      link.href = href;
      document.head.append(link);
    }
  }
  if (document.head.querySelector('meta[name="theme-color"]') === null) {
    const meta = document.createElement('meta');
    meta.name = 'theme-color';
    meta.content = THEME_COLOR;
    document.head.append(meta);
  }
}
