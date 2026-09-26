/**
 * The pieces of a detail page's hero, spelled out whole for the Tailwind scanner: the three
 * detail pages of this feature share them.
 */
export const HERO_STYLES = {
  /** The entry's name, as large as the viewport allows. */
  name: 'font-beaufort text-[clamp(2.4rem,6vw,4.5rem)] leading-[0.95] tracking-[0.04em] text-balance uppercase',
  /** The entry's art, blurred into an ambient glow behind the hero. */
  echo: 'pointer-events-none absolute top-1/2 -end-24 aspect-square w-[30rem] max-w-[70vw] -translate-y-1/2 object-contain opacity-12 blur-[34px] saturate-150 select-none',
  /** The kind of entry, its version and language, above the name. */
  eyebrow: 'mb-2.5 flex flex-wrap items-center eyebrow',
} as const;
