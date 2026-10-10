// Pinning rules of the actions a workflow runs. A tag can be moved by the owner of the action,
// a commit cannot: every remote action and reusable workflow is pinned to a full commit SHA,
// its version kept in a trailing comment for the reader and for Dependabot, which bumps both.
// Local actions and reusable workflows (`./…`) are code of this repository, reviewed with it;
// a container image is pinned to its digest.

const LOCAL_PREFIX = './';
const DOCKER_PREFIX = 'docker://';
const FULL_COMMIT_SHA = /^[^@\s]+@[0-9a-f]{40}$/;
const IMAGE_DIGEST = /@sha256:[0-9a-f]{64}$/;
// `uses:` as a step key, a job key or the first key of a list item; the value may be quoted
// and followed by a comment.
const USES_LINE =
  /^\s*(?:-\s+)?uses:\s*(?<value>"[^"]*"|'[^']*'|[^\s#]+)\s*(?:#\s*(?<comment>.*?))?\s*$/;
// Any other mention of a `uses` key (flow mapping, value on the next line, a script line, and
// the spellings YAML reads as the same key: quoted, spaced before the colon, explicit `? uses`,
// any case): reported rather than skipped, so that no form of reference escapes the check.
const USES_MENTION = /(?:^|[\s{,])["']?uses["']?\s*:|^\s*(?:-\s+)?\?\s+["']?uses["']?\s*$/i;
const COMMENT_LINE = /^\s*#/;
const QUOTES = /^["']|["']$/g;

export const PROBLEMS = Object.freeze({
  notPinned: 'not pinned to a full commit SHA',
  noVersionComment: 'pinned without its version in a trailing comment',
  imageNotPinned: 'container image not pinned to a sha256 digest',
  unrecognised: 'not written as `uses: owner/repo@<40 hex> # vX.Y.Z` on one line',
});

function referenceOf(content, line) {
  const match = USES_LINE.exec(content);
  if (match) {
    const value = match.groups.value.replace(QUOTES, '');
    return { line, value, comment: match.groups.comment ?? '' };
  }
  return USES_MENTION.test(content) ? { line, value: content.trim(), comment: null } : null;
}

/**
 * The `uses:` references of a workflow or action file, with their 1-based line. A reference
 * whose line could not be parsed has a null comment.
 */
export function usesReferencesOf(text) {
  return text
    .split(/\r?\n/)
    .map((content, index) => (COMMENT_LINE.test(content) ? null : referenceOf(content, index + 1)))
    .filter((reference) => reference !== null);
}

/** Why a reference breaks the pinning rules, or null when it follows them. */
export function pinningProblemOf({ value, comment }) {
  if (comment === null) {
    return PROBLEMS.unrecognised;
  }
  if (value.startsWith(LOCAL_PREFIX)) {
    return null;
  }
  if (value.startsWith(DOCKER_PREFIX)) {
    return IMAGE_DIGEST.test(value) ? null : PROBLEMS.imageNotPinned;
  }
  if (!FULL_COMMIT_SHA.test(value)) {
    return PROBLEMS.notPinned;
  }
  return comment.trim() === '' ? PROBLEMS.noVersionComment : null;
}

/** The references of a file that break the rules, each with its problem. */
export function pinningProblemsOf(text) {
  return usesReferencesOf(text).flatMap((reference) => {
    const problem = pinningProblemOf(reference);
    return problem ? [{ ...reference, problem }] : [];
  });
}
