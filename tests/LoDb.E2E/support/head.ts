import type { Page } from '@playwright/test';

/** An hreflang alternate of the page. */
export interface Alternate {
  readonly lang: string;
  readonly href: string;
}

/** The head of a page as a crawler reads it. */
export interface Head {
  readonly title: string;
  readonly description: string | null;
  readonly robots: string | null;
  readonly canonicals: readonly string[];
  readonly alternates: readonly Alternate[];
  /** The JSON-LD blocks, parsed; a block that does not parse counts in `jsonLdErrors`. */
  readonly jsonLd: readonly unknown[];
  readonly jsonLdErrors: number;
}

/** Reads the head of the document; with JavaScript disabled, the head the server wrote. */
export async function readHead(page: Page): Promise<Head> {
  return page.evaluate(() => {
    const head = document.head;
    const content = (selector: string): string | null =>
      head.querySelector(selector)?.getAttribute('content') ?? null;
    const blocks = [...head.querySelectorAll('script[type="application/ld+json"]')];
    const parsed = blocks.map((block) => {
      try {
        return JSON.parse(block.textContent ?? '') as unknown;
      } catch {
        return undefined;
      }
    });
    return {
      title: document.title,
      description: content('meta[name="description"]'),
      robots: content('meta[name="robots"]'),
      canonicals: [...head.querySelectorAll('link[rel="canonical"]')].map(
        (link) => link.getAttribute('href') ?? '',
      ),
      alternates: [...head.querySelectorAll('link[rel="alternate"][hreflang]')].map((link) => ({
        lang: link.getAttribute('hreflang') ?? '',
        href: link.getAttribute('href') ?? '',
      })),
      jsonLd: parsed.filter((block) => block !== undefined),
      jsonLdErrors: parsed.filter((block) => block === undefined).length,
    };
  });
}

function nodesOf(value: unknown): Record<string, unknown>[] {
  if (Array.isArray(value)) return value.flatMap(nodesOf);
  if (typeof value !== 'object' || value === null) return [];
  const node = value as Record<string, unknown>;
  return [node, ...Object.values(node).flatMap(nodesOf)];
}

/** Every `@type` of the JSON-LD, nested ones included. */
export function typesOf(blocks: readonly unknown[]): Set<string> {
  return new Set(
    nodesOf(blocks).flatMap((node) => {
      const type = node['@type'];
      return Array.isArray(type) ? type.map(String) : type === undefined ? [] : [String(type)];
    }),
  );
}

/** The nodes of a type in the JSON-LD, nested ones included. */
export function nodesOfType(blocks: readonly unknown[], type: string): Record<string, unknown>[] {
  return nodesOf(blocks).filter((node) => node['@type'] === type);
}

/** The paths of the empty fields of the JSON-LD (ADR 0005 removes them): '', null, [], {}. */
export function emptyFields(value: unknown, path = '$'): string[] {
  if (value === null || value === '') return [path];
  if (Array.isArray(value)) {
    if (value.length === 0) return [path];
    return value.flatMap((item, index) => emptyFields(item, `${path}[${index}]`));
  }
  if (typeof value !== 'object') return [];
  const entries = Object.entries(value as Record<string, unknown>);
  if (entries.length === 0) return [path];
  return entries.flatMap(([key, item]) => emptyFields(item, `${path}.${key}`));
}
