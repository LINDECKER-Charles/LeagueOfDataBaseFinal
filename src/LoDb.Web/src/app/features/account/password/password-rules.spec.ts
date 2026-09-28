import { PASSWORD_MIN_LENGTH, passwordRules, passwordsMatch } from './password-rules';

type RuleName = 'length' | 'lowercase' | 'uppercase' | 'digit' | 'special';

function ruleMap(password: string): Record<RuleName, boolean> {
  const rules = passwordRules(password).map((rule) => [
    rule.code.replace('auth.password.rule_', ''),
    rule.met,
  ]);
  return Object.fromEntries(rules) as Record<RuleName, boolean>;
}

describe('passwordRules', () => {
  it('meets every rule with a password the server accepts', () => {
    expect(ruleMap('Corr3ct-horse-Battery!')).toEqual({
      length: true,
      lowercase: true,
      uppercase: true,
      digit: true,
      special: true,
    });
  });

  it('breaks every rule with the empty string', () => {
    expect(ruleMap('')).toEqual({
      length: false,
      lowercase: false,
      uppercase: false,
      digit: false,
      special: false,
    });
  });

  it('lists the rules in the order of the server, under its codes', () => {
    expect(passwordRules('').map((rule) => rule.code)).toEqual([
      'auth.password.rule_length',
      'auth.password.rule_lowercase',
      'auth.password.rule_uppercase',
      'auth.password.rule_digit',
      'auth.password.rule_special',
    ]);
  });

  it('flags each missing rule on its own', () => {
    expect(ruleMap('alllowercase1!x').uppercase).toBe(false);
    expect(ruleMap('ALLUPPERCASE1!X').lowercase).toBe(false);
    expect(ruleMap('NoDigitsHere!!').digit).toBe(false);
    expect(ruleMap('NoSpecial12345').special).toBe(false);
  });

  it('counts the length in code points, not UTF-16 units', () => {
    // 11 emoji are 22 UTF-16 units but 11 code points: still too short.
    expect(ruleMap('💎'.repeat(PASSWORD_MIN_LENGTH - 1)).length).toBe(false);
    expect(ruleMap('💎'.repeat(PASSWORD_MIN_LENGTH)).length).toBe(true);
  });

  it('takes anything but a letter or a number as special: punctuation, space, symbol', () => {
    for (const candidate of ['with space', 'semi;colon', 'em—dash', 'caret^']) {
      expect(ruleMap(candidate).special, candidate).toBe(true);
    }
    expect(ruleMap('OnlyAlnum123').special).toBe(false);
  });

  it('knows accented letters as lowercase or uppercase', () => {
    expect(ruleMap('été').lowercase).toBe(true);
    expect(ruleMap('ÉTÉ').uppercase).toBe(true);
  });

  it('counts only ASCII digits, as the server does', () => {
    // Arabic-Indic digits are numbers, so neither a digit nor a special character.
    expect(ruleMap('٣٤٥')).toMatchObject({ digit: false, special: false });
    expect(ruleMap('7').digit).toBe(true);
  });
});

describe('passwordsMatch', () => {
  it('holds for identical values only, and never for two empty ones', () => {
    expect(passwordsMatch('Abc-1234-defg', 'Abc-1234-defg')).toBe(true);
    expect(passwordsMatch('Abc-1234-defg', 'Abc-1234-defG')).toBe(false);
    expect(passwordsMatch('', '')).toBe(false);
  });
});
