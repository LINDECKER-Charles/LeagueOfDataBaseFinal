import { jsonLdScriptText } from './json-ld-script-text';

describe('jsonLdScriptText', () => {
  it('hands the text over as it is where the browser has no Trusted Types', () => {
    expect(jsonLdScriptText(null)('{"@type":"Thing"}')).toBe('{"@type":"Thing"}');
  });

  it("goes through the site's own policy, for JSON only, where the browser has them", () => {
    const trusted = { script: true };
    const createPolicy = vi.fn((_name: string, rules: { createScript(text: string): string }) => ({
      createScript: (text: string) => (rules.createScript(text) === text ? trusted : null),
    }));
    const view = { trustedTypes: { createPolicy } } as unknown as Window;

    const text = jsonLdScriptText(view);

    expect(text('{"@type":"Thing"}')).toBe(trusted);
    expect(createPolicy).toHaveBeenCalledExactlyOnceWith('lodb#json-ld', expect.anything());
    expect(() => text('alert(1)')).toThrow(SyntaxError);
  });
});
