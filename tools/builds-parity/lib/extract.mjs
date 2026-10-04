// What a reader sees on a /b/{token} page, read from its DOM. Runs IN THE PAGE (Playwright's
// page.evaluate): it must stay self-contained, without imports or outer variables. Both
// stacks share the class names it reads (app/templates/build/show.html.twig and
// src/LoDb.Web/src/app/features/builds/share): .bshare-head, .bshare-portrait, .hx-chip,
// .bshare-tree, .keystone__icon, .bsteps-node, .bshare-item, .forge-ghost, .vote-box.

/**
 * The facts of the page, as displayed: the champion, the mode's label, the patch, the score
 * of a public build, each rune tree with its keystone and perks, each step with its cost and
 * items, and the total. A ghost is marked; its name is the fallback it shows (a ghost item's
 * first letters, its title being "unavailable"). Null when the page shows no build.
 */
export function extractFacts() {
  const text = (element) => (element?.textContent ?? '').replace(/\s+/g, ' ').trim();
  const ghost = (element) => element?.classList.contains('forge-ghost') ?? false;
  const number = (value) => {
    const digits = value.replace(/[^\d-]/g, '');
    return digits === '' ? null : Number(digits);
  };
  const head = document.querySelector('.bshare-head');
  if (head === null) return null;
  const chips = [...head.querySelectorAll('.hx-chip')];
  const versions = chips.map((chip) => text(chip).match(/\d+\.\d+(?:\.\d+)*/g)).find(Boolean);
  const score = head.querySelector('.vote-box .vote-score');
  const perk = (item) => ({ name: text(item.querySelector('.truncate')), ghost: ghost(item) });
  const tree = (article) => {
    const icon = article.querySelector('.keystone__icon');
    const keystone = icon && {
      name: text(icon.parentElement?.querySelector('.keystone__icon + div p.font-beaufort')),
      ghost: ghost(icon),
    };
    const perks = [...article.querySelectorAll('ul > li')].map(perk);
    return { tree: text(article.querySelector('.bshare-tree')), keystone, perks };
  };
  const item = (tile) =>
    ghost(tile)
      ? { name: text(tile).slice(0, 2).toUpperCase(), ghost: true }
      : { name: tile.getAttribute('title') ?? '', ghost: false };
  const step = (node) => ({
    label: text(node.querySelector('h3')),
    cost: number(text(node.querySelector('header .hx-chip'))),
    items: [...node.querySelectorAll('.bshare-item')].map(item),
  });
  return {
    name: text(head.querySelector('h1')),
    champion: {
      name: text(head.querySelector('.eyebrow')).split('·')[0].trim(),
      ghost: ghost(head.querySelector('.bshare-portrait')),
    },
    mode: text(chips.find((chip) => chip.hasAttribute('title'))),
    patch: { version: versions?.[0] ?? null, current: versions?.[1] ?? null },
    vote: score === null ? null : number(text(score)),
    runes: [...document.querySelectorAll('article:has(> .bshare-tree)')].map(tree),
    steps: [...document.querySelectorAll('.bsteps-node')].map(step),
    total: number(text(document.querySelector('.hx-plate__value'))),
  };
}
