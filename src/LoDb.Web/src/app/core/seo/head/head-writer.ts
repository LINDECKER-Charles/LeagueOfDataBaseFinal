import { DOCUMENT, Injectable, inject } from '@angular/core';
import { Title } from '@angular/platform-browser';
import type { JsonLdNode } from '../json-ld/core/json-ld-node';
import { serializeJsonLd } from '../json-ld/core/serialize-json-ld';
import type { HeadTags } from './head-tags';

/** Marks the elements the writer owns, so the next page replaces them all. */
const MARKER = 'data-lodb-seo';
const JSON_LD_TYPE = 'application/ld+json';

/**
 * Writes {@link HeadTags} into `<head>`, replacing whatever the previous page wrote: a tag a
 * page does not state (a canonical on an error page) must not survive from the last one.
 */
@Injectable({ providedIn: 'root' })
export class HeadWriter {
  private readonly document = inject(DOCUMENT);
  private readonly title = inject(Title);

  write(tags: HeadTags): void {
    const head = this.document.head;
    head.querySelectorAll(`[${MARKER}]`).forEach((element) => element.remove());
    this.title.setTitle(tags.title);
    const elements = [
      ...tags.metas.map((meta) =>
        this.element('meta', { [meta.attribute]: meta.key, content: meta.content }),
      ),
      ...tags.links.map((link) => this.element('link', { ...link })),
      ...tags.jsonLd.map((node) => this.script(node)),
    ];
    elements.forEach((element) => head.appendChild(element));
  }

  private element(tag: 'meta' | 'link', attributes: Record<string, string | undefined>): Element {
    const element = this.document.createElement(tag);
    element.setAttribute(MARKER, '');
    for (const [name, value] of Object.entries(attributes)) {
      if (value !== undefined) {
        element.setAttribute(name, value);
      }
    }
    return element;
  }

  // The serializer escapes `<`: the text can never close the element it sits in.
  private script(node: JsonLdNode): Element {
    const script = this.document.createElement('script');
    script.setAttribute(MARKER, '');
    script.type = JSON_LD_TYPE;
    script.textContent = serializeJsonLd(node);
    return script;
  }
}
