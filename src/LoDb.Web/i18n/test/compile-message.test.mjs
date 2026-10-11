import assert from 'node:assert/strict';
import { describe, test } from 'node:test';
import { compileMessage } from '../lib/compile-message.mjs';

const count = ['#', 'count', 0];

describe('compileMessage', () => {
  test('keeps a message without ICU syntax as its text', () => {
    assert.equal(compileMessage('Items'), 'Items');
    assert.equal(compileMessage("L'objet de Kai'Sa"), "L'objet de Kai'Sa");
    assert.equal(compileMessage(''), '');
  });

  test('keeps a message with Transloco parameters for Transloco', () => {
    assert.equal(
      compileMessage('Patch {{ version }} — {{name}}'),
      'Patch {{ version }} — {{name}}',
    );
    assert.equal(compileMessage("'{{ name }}' l'a"), "'{{ name }}' l'a");
  });

  test('reads ICU quoting as MessageFormat does', () => {
    assert.equal(compileMessage("It''s '{literal}'"), "It's {literal}");
  });

  test('compiles arguments, plurals with exact cases and #', () => {
    assert.deepEqual(compileMessage('{count, plural, =1 {# result} other {# results}}'), [
      ['plural', 'count', 0, { 1: [count, ' result'], other: [count, ' results'] }],
    ]);
    assert.deepEqual(compileMessage('{day} {hour} h'), [
      ['arg', 'day'],
      ' ',
      ['arg', 'hour'],
      ' h',
    ]);
  });

  test('compiles offsets, ordinals and selects, # standing for the plural around it', () => {
    assert.deepEqual(
      compileMessage(
        '{n, plural, offset:1 =0 {none} other {{who, select, me {# more} other {#}}}}',
      ),
      [
        [
          'plural',
          'n',
          1,
          {
            0: ['none'],
            other: [['select', 'who', { me: [['#', 'n', 1], ' more'], other: [['#', 'n', 1]] }]],
          },
        ],
      ],
    );
    assert.deepEqual(compileMessage('{n, selectordinal, one {#st} other {#th}}'), [
      ['selectordinal', 'n', 0, { one: [['#', 'n', 0], 'st'], other: [['#', 'n', 0], 'th'] }],
    ]);
    assert.deepEqual(compileMessage('# {n}'), ['# ', ['arg', 'n']]);
  });

  test('refuses what the site cannot render, or MessageFormat would refuse', () => {
    assert.throws(() => compileMessage('{n, number}'), /formatter/);
    assert.throws(() => compileMessage('{n, plural, one {#}}'), /no 'other' case/);
    assert.throws(() => compileMessage('{kind, select, a {A}}'), /no 'other' case/);
    assert.throws(() => compileMessage('{unclosed'));
    assert.throws(
      () => compileMessage('{{ name }}: {count, plural, other {#}}'),
      /mixes \{\{ \}\} parameters with ICU syntax/,
    );
  });
});
