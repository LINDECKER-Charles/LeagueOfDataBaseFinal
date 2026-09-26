import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { escapeIcuLiteral } from '../../lib/convert/message/escape-icu-literal.mjs';
import { renderLikeTransloco } from '../support/render-like-transloco.mjs';

const ALPHABET = ['a', "'", '{', '}', '#', ' '];
const MAX_LENGTH = 5;

function* everyText(length, prefix = '') {
  if (length === 0) {
    yield prefix;
    return;
  }
  for (const char of ALPHABET) {
    yield* everyText(length - 1, prefix + char);
  }
}

// Literal `{{` and `}}` never reach the escaper: tokenizeMessage refuses them.
function* allTexts() {
  for (let length = 1; length <= MAX_LENGTH; length += 1) {
    for (const text of everyText(length)) {
      if (!text.includes('{{') && !text.includes('}}')) {
        yield text;
      }
    }
  }
}

describe('escapeIcuLiteral', () => {
  it('keeps ordinary apostrophes readable', () => {
    assert.equal(escapeIcuLiteral("l'objet d'Ornn"), "l'objet d'Ornn");
  });

  it('round-trips every text made of ICU syntax at the end of a message', () => {
    for (const text of allTexts()) {
      assert.equal(renderLikeTransloco(escapeIcuLiteral(text), {}, 'en'), text, text);
    }
  });

  it('round-trips every such text right before an argument', () => {
    for (const text of allTexts()) {
      const message = `${escapeIcuLiteral(text, { followedBySyntax: true })}{{ name }}`;
      assert.equal(renderLikeTransloco(message, { name: 'X' }, 'en'), `${text}X`, text);
    }
  });

  it('round-trips every such text inside a plural branch', () => {
    for (const text of allTexts()) {
      const branch = escapeIcuLiteral(text, { inPlural: true, followedBySyntax: true });
      const message = `{count, plural, other {${branch}}}`;
      assert.equal(renderLikeTransloco(message, { count: 2 }, 'en'), text, text);
    }
  });
});
