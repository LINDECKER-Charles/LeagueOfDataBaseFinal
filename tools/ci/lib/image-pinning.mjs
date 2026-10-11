// Pinning rules of the images a Dockerfile builds from. A tag can be moved by whoever publishes
// the image, a digest cannot: every FROM, every `--from` that names an image rather than a stage,
// and the `# syntax=` frontend read `image:tag@sha256:<64 hex>`. The tag stays for the reader
// and for Dependabot, which bumps both together; a variable would hide the image from both.

const DIGEST = /@sha256:[0-9a-f]{64}$/;
const PARSER_DIRECTIVE = /^#\s*(?<name>[a-z]+)\s*=\s*(?<value>\S*)\s*$/i;
const SYNTAX = 'syntax';
const ESCAPE = 'escape';
// The line continuation character, which the `escape` directive picks among these two.
const CONTINUATIONS = Object.freeze({ '\\': /\\\s*$/, '`': /`\s*$/ });
const DEFAULT_ESCAPE = '\\';
const COMMENT_LINE = /^\s*#/;
const INSTRUCTION = /^(?<keyword>[a-z]+)\s+(?<rest>.*)$/i;
const FLAG = /^--/;
const STAGE_ALIAS = /^as$/i;
const COPY_FROM = /(?:^|\s)--from=(?<value>\S+)/gi;
const MOUNT_FROM = /--mount=\S*?\bfrom=(?<value>[^,\s]+)/gi;
// `--from` of COPY and ADD; `from=` of a RUN's cache or bind mount.
const FROM_FLAGS = Object.freeze({ COPY: COPY_FROM, ADD: COPY_FROM, RUN: MOUNT_FROM });
const STAGE_INDEX = /^\d+$/;
const SCRATCH = 'scratch';
const VARIABLE = '$';

export const PROBLEMS = Object.freeze({
  notPinned: 'not pinned to a sha256 digest',
  noTag: 'pinned without its tag, which the reader and Dependabot need',
  variable: 'named through a variable, which neither this check nor Dependabot can follow',
});

// Parser directives, name → { line, value }: they only count before the first comment, blank
// line or instruction, and the first of a name wins.
function directivesOf(lines) {
  const directives = new Map();
  for (const [index, content] of lines.entries()) {
    const directive = PARSER_DIRECTIVE.exec(content.trim());
    if (!directive) {
      break;
    }
    const name = directive.groups.name.toLowerCase();
    if (!directives.has(name)) {
      directives.set(name, { line: index + 1, value: directive.groups.value });
    }
  }
  return directives;
}

// With `# escape=` + backtick, a backslash ends no line: reading it as a continuation would
// fold the next instruction, a FROM included, into the previous one. BuildKit refuses any
// other escape character.
function continuationOf(directives) {
  const escape = directives.get(ESCAPE)?.value ?? DEFAULT_ESCAPE;
  return CONTINUATIONS[escape] ?? CONTINUATIONS[DEFAULT_ESCAPE];
}

// One entry per instruction, its continued lines joined; comments are dropped, even between
// continued lines, as BuildKit does.
function instructionsOf(lines, continuation) {
  const instructions = [];
  let current = null;
  for (const [index, content] of lines.entries()) {
    if (COMMENT_LINE.test(content) || (current === null && content.trim() === '')) {
      continue;
    }
    current ??= { line: index + 1, text: '' };
    current.text = `${current.text} ${content.replace(continuation, '')}`.trim();
    if (!continuation.test(content)) {
      instructions.push(current);
      current = null;
    }
  }
  return current === null ? instructions : [...instructions, current];
}

function fromOf(rest) {
  const words = rest.split(/\s+/).filter((word) => !FLAG.test(word));
  const alias = STAGE_ALIAS.test(words[1] ?? '') ? words[2] : undefined;
  return { image: words[0] ?? '', alias: alias?.toLowerCase() };
}

function isStageOrScratch(value, stages) {
  const name = value.toLowerCase();
  return name === SCRATCH || stages.has(name) || STAGE_INDEX.test(name);
}

function imagesOfInstruction({ keyword, rest }, stages) {
  if (keyword === 'FROM') {
    const { image, alias } = fromOf(rest);
    const images = isStageOrScratch(image, stages) ? [] : [image];
    if (alias) {
      stages.add(alias);
    }
    return images;
  }
  const pattern = FROM_FLAGS[keyword];
  if (!pattern) {
    return [];
  }
  return [...rest.matchAll(pattern)]
    .map((match) => match.groups.value)
    .filter((value) => !isStageOrScratch(value, stages));
}

/** The images a Dockerfile names (frontend, bases, `--from` images), with their 1-based line. */
export function imageReferencesOf(text) {
  const lines = text.split(/\r?\n/);
  const directives = directivesOf(lines);
  const syntax = directives.get(SYNTAX) ?? null;
  const instructions = instructionsOf(lines, continuationOf(directives));
  const stages = new Set();
  const references = instructions.flatMap(({ line, text: instruction }) => {
    const match = INSTRUCTION.exec(instruction);
    if (!match) {
      return [];
    }
    const parsed = { keyword: match.groups.keyword.toUpperCase(), rest: match.groups.rest };
    return imagesOfInstruction(parsed, stages).map((value) => ({ line, value }));
  });
  return syntax === null ? references : [syntax, ...references];
}

/** Why an image reference breaks the pinning rules, or null when it follows them. */
export function pinningProblemOf(value) {
  if (value.includes(VARIABLE)) {
    return PROBLEMS.variable;
  }
  if (!DIGEST.test(value)) {
    return PROBLEMS.notPinned;
  }
  const name = value.slice(0, value.indexOf('@'));
  return name.slice(name.lastIndexOf('/') + 1).includes(':') ? null : PROBLEMS.noTag;
}

/** The image references of a Dockerfile that break the rules, each with its problem. */
export function pinningProblemsOf(text) {
  return imageReferencesOf(text).flatMap((reference) => {
    const problem = pinningProblemOf(reference.value);
    return problem ? [{ ...reference, problem }] : [];
  });
}
