// Runs the maintenance page's scripts (docker/nginx/maintenance/assets) as its <head> does, in a
// context that stands in for the browser: the document's elements the scripts write to, the
// visitor's cookies, languages and clock, the address, and the answers of /maintenance/status.json.
import { readFileSync } from 'node:fs';
import vm from 'node:vm';

const ASSETS = new URL('../../../docker/nginx/maintenance/assets/', import.meta.url);
const SCRIPTS = ['messages.js', 'maintenance.js'].map((name) =>
  readFileSync(new URL(name, ASSETS), 'utf8'),
);
// The elements of index.html that maintenance.js fills.
const MESSAGE_KEYS = ['heading', 'lead', 'autoReload', 'retry'];
const OK_STATUSES = { from: 200, to: 299 };

function element(attributes = {}) {
  const values = { ...attributes };
  return {
    textContent: '',
    getAttribute: (name) => values[name] ?? null,
    setAttribute: (name, value) => {
      values[name] = String(value);
    },
  };
}

// A fetch Response as the script reads it: its status, its Date header, its JSON body.
function answerOf({ status, body, date }) {
  return {
    status,
    ok: status >= OK_STATUSES.from && status <= OK_STATUSES.to,
    headers: { get: (name) => (name.toLowerCase() === 'date' ? (date ?? null) : null) },
    json: async () => body,
  };
}

function documentOf(cookie, parts) {
  const selected = {
    'meta[name="theme-color"]': parts.meta,
    '[data-estimate-label]': parts.label,
    '[data-estimate-value]': parts.value,
  };
  return {
    documentElement: parts.root,
    cookie,
    title: '',
    readyState: 'loading',
    querySelector: (selector) => selected[selector] ?? null,
    querySelectorAll: (selector) => (selector === '[data-message]' ? parts.messages : []),
    addEventListener: (type, listener) => {
      parts.listeners[type] = listener;
    },
  };
}

// The visitor's clock, `clientNow` milliseconds since the epoch, whatever the machine's.
function clockAt(clientNow) {
  return class VisitorDate extends Date {
    static now() {
      return clientNow;
    }
  };
}

/**
 * Opens the page at `path` for a visitor with `cookie`, `languages` and a clock at `clientNow`
 * (ms); `status()` gives each answer of the deploy's state ({ status, body, date }), or throws
 * for a request that never reaches nginx. `load()` fires DOMContentLoaded and lets the first poll
 * settle; `tick()` runs the next poll.
 */
export function openPage({ path = '/en/', cookie = '', languages = ['en-US'], clientNow, status }) {
  const parts = {
    root: element(),
    meta: element({ name: 'theme-color', content: '#010a13' }),
    label: element(),
    value: element(),
    messages: MESSAGE_KEYS.map((key) => element({ 'data-message': key })),
    listeners: {},
  };
  const polls = [];
  const reloads = [];
  const settle = () => new Promise((resolve) => setTimeout(resolve, 0));
  const textOf = (message) => [message.getAttribute('data-message'), message.textContent];
  const context = {
    document: documentOf(cookie, parts),
    navigator: { languages },
    location: { pathname: path, reload: () => reloads.push(path) },
    fetch: async () => answerOf(status()),
    setInterval: (poll) => polls.push(poll),
    Date: clockAt(clientNow ?? Date.now()),
  };
  context.window = context;
  vm.createContext(context);
  SCRIPTS.forEach((script) => vm.runInContext(script, context));
  return {
    ...parts,
    document: context.document,
    reloads,
    texts: () => Object.fromEntries(parts.messages.map(textOf)),
    load: async () => {
      parts.listeners.DOMContentLoaded();
      await settle();
    },
    tick: async () => {
      polls.forEach((poll) => poll());
      await settle();
    },
  };
}
