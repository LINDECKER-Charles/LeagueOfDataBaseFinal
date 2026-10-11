import { type InlineScript, inlineScripts } from './inline-scripts';
import type { PageScripts } from './page-scripts';
import { scriptHash } from './script-hash';

// Angular writes the event dispatch contract into the index template and, on a page with
// listeners, the call that replays the events of the first moments right after it. The call's
// arguments are the application id and the event types the page listens to (platform-server's
// insertEventRecordScript): its shape is fixed, to be checked again at each Angular upgrade.
const CONTRACT_ID = /(?:^|\s)id\s*=\s*["']?ng-event-dispatch-contract["']?(?=\s|$)/i;
const EVENT_TYPES = String.raw`\[(?:"[a-z]+"(?:,"[a-z]+")*)?\]`;
const REPLAY = new RegExp(
  String.raw`^window\.__jsaction_bootstrap\(document\.body,"[\w-]+",` +
    `${EVENT_TYPES},${EVENT_TYPES}` +
    String.raw`\);$`,
);

/**
 * The inline scripts a page may run: those the build wrote into its index templates (the
 * theme, the event dispatch contract, the deferred stylesheets' loader), and the event replay
 * call when it directly follows the contract. Any other inline script in a rendered page was
 * never written by the application, and the policy leaves it out, so the browser refuses to
 * run it: a script slipped into the HTML on the server (stored content, a template mistake)
 * gets no hash of its own.
 */
export class KnownInlineScripts {
  private readonly hashes: ReadonlyMap<string, string>;

  /** `templates`: the index templates of the build, as it wrote them. */
  constructor(templates: readonly string[]) {
    const scripts = templates.flatMap((template) => inlineScripts(template));
    this.hashes = new Map(scripts.map(({ text }) => [text, scriptHash(text)]));
  }

  /** The hashes the policy of `html` admits, and the count of the scripts it leaves out. */
  of(html: string): PageScripts {
    const hashes = new Set<string>();
    let unexpected = 0;
    let previous: InlineScript | undefined;
    for (const script of inlineScripts(html)) {
      const hash = this.hashOf(script, previous);
      if (hash === undefined) {
        unexpected += 1;
      } else {
        hashes.add(hash);
      }
      previous = script;
    }
    return { hashes: [...hashes], unexpected };
  }

  private hashOf(script: InlineScript, previous: InlineScript | undefined): string | undefined {
    const known = this.hashes.get(script.text);
    if (known !== undefined) {
      return known;
    }
    return this.isReplay(script, previous) ? scriptHash(script.text) : undefined;
  }

  private isReplay(script: InlineScript, previous: InlineScript | undefined): boolean {
    return (
      previous !== undefined &&
      previous.end === script.start &&
      this.hashes.has(previous.text) &&
      CONTRACT_ID.test(previous.attributes) &&
      REPLAY.test(script.text)
    );
  }
}
