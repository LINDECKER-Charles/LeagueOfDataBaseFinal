import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { convertMessage } from '../../lib/convert/message/convert-message.mjs';
import { renderLikeTransloco } from '../support/render-like-transloco.mjs';

const converted = (source, locale = 'en') => convertMessage(source, locale).message;

describe('convertMessage', () => {
  it('turns every %param% into a Transloco parameter', () => {
    assert.equal(
      converted('Hi %name%, bye %name% (%n%)'),
      'Hi {{ name }}, bye {{ name }} ({{ n }})',
    );
  });

  it('leaves a percent that is no placeholder alone', () => {
    assert.equal(converted('100% free, 20 % off'), '100% free, 20 % off');
  });

  it('keeps a message rendering what Symfony rendered', () => {
    const message = converted("L'objet %name% {rare} #1 de %name%");
    assert.equal(
      renderLikeTransloco(message, { name: 'Ornn' }, 'fr'),
      "L'objet Ornn {rare} #1 de Ornn",
    );
  });

  it('converts a pipe plural into ICU with the categories of the locale', () => {
    assert.equal(
      converted('One apple|%count% apples'),
      '{count, plural, one {One apple} other {# apples}}',
    );
    assert.equal(
      converted('%count% яблоко|%count% яблока|%count% яблок', 'ru'),
      '{count, plural, one {# яблоко} few {# яблока} other {# яблок}}',
    );
  });

  it('keeps Russian counts on the Symfony forms', () => {
    const message = converted('%count% яблоко|%count% яблока|%count% яблок', 'ru');
    const render = (count) => renderLikeTransloco(message, { count }, 'ru');
    assert.deepEqual([1, 3, 5, 21].map(render), ['1 яблоко', '3 яблока', '5 яблок', '21 яблоко']);
  });

  it('converts explicit sets, closed and unbounded intervals, and ignores labels', () => {
    assert.equal(
      converted('{0} None|[1,1] One|]1,Inf[ %count% for %name%'),
      '{count, plural, =0 {None} =1 {One} other {# for {name}}}',
    );
    assert.equal(
      converted('one: One pear|other: %count% pears'),
      '{count, plural, one {One pear} other {# pears}}',
    );
  });

  it('refuses an interval ICU cannot express', () => {
    assert.throws(() => converted('[1,5] Few|]5,Inf[ %count% many'), /no ICU equivalent/);
  });

  it('reads || as a literal pipe, and a pipe without %count% as text', () => {
    assert.equal(
      converted('One || apple|%count% || apples'),
      '{count, plural, one {One | apple} other {# | apples}}',
    );
    assert.equal(converted('Left | right'), 'Left | right');
  });

  it('refuses literal Transloco delimiters', () => {
    assert.throws(() => converted('A {{ literal }} brace'), /Transloco interpolation/);
  });
});
