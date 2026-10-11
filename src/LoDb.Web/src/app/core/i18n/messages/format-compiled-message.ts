import type { CompiledMessage } from './compiled-message';
import type { LocaleFormats } from './locale-formats';

type Params = Readonly<Record<string, unknown>>;
type Part = CompiledMessage[number];
type Cases = Readonly<Record<string, CompiledMessage>>;
type PluralPart = Extract<Part, readonly ['plural' | 'selectordinal', ...unknown[]]>;

const OTHER = 'other';

// An exact case (`=1`, stored as `1`) wins over the categories, as in MessageFormat: it is
// looked up with the parameter itself, before any offset.
function exactCase(cases: Cases, value: unknown): CompiledMessage | undefined {
  const key = String(value);
  return Object.hasOwn(cases, key) ? cases[key] : undefined;
}

function categoryCase(cases: Cases, category: string): CompiledMessage {
  return Object.hasOwn(cases, category) ? cases[category] : cases[OTHER];
}

function pluralCase(part: PluralPart, params: Params, formats: LocaleFormats): CompiledMessage {
  const [kind, name, offset, cases] = part;
  const value = params[name];
  const count = Number(value) - offset;
  const category = kind === 'plural' ? formats.cardinal(count) : formats.ordinal(count);
  return exactCase(cases, value) ?? categoryCase(cases, category);
}

function formatPart(part: Part, params: Params, formats: LocaleFormats): string {
  if (typeof part === 'string') {
    return part;
  }
  switch (part[0]) {
    case 'arg':
      return String(params[part[1]]);
    case '#':
      return formats.number(Number(params[part[1]]) - part[2]);
    case 'select':
      return formatCompiledMessage(
        exactCase(part[2], params[part[1]]) ?? part[2][OTHER],
        params,
        formats,
      );
    default:
      return formatCompiledMessage(pluralCase(part, params, formats), params, formats);
  }
}

/**
 * Renders a message the build compiled (`CompiledMessage`) with its parameters, the way
 * MessageFormat renders its ICU source: arguments as they are, `#` formatted for the locale,
 * an exact plural case before the locale's category, `other` when no case matches.
 */
export function formatCompiledMessage(
  message: CompiledMessage,
  params: Params,
  formats: LocaleFormats,
): string {
  return message.map((part) => formatPart(part, params, formats)).join('');
}
