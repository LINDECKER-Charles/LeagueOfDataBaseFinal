import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { RichText } from './rich-text';
import { richTextHtml } from './rich-text-html';

describe('richTextHtml', () => {
  it("keeps the game's vocabulary, lower-cased and without attributes", () => {
    expect(richTextHtml('<mainText><stats>+10 <attention>AP</attention></stats></mainText>')).toBe(
      '<maintext><stats>+10 <attention>AP</attention></stats></maintext>',
    );
    expect(
      richTextHtml(
        '<lol-uikit-tooltipped-keyword key="LinkTooltip">x</lol-uikit-tooltipped-keyword>',
      ),
    ).toBe('<lol-uikit-tooltipped-keyword>x</lol-uikit-tooltipped-keyword>');
  });

  it('writes line breaks as void tags', () => {
    expect(richTextHtml('a<br>b<br />c</br>')).toBe('a<br>b<br>c');
  });

  it('drops any other tag, keeping its text', () => {
    expect(richTextHtml('<font color="#FF0000">Red</font> <a href="https://x">link</a>')).toBe(
      'Red link',
    );
  });

  it('never lets a script, a handler or a URL through', () => {
    const html = richTextHtml(
      '<script>alert(1)</script><img src=x onerror=alert(1)><b onclick="alert(1)">bold</b>',
    );

    expect(html).toBe('alert(1)<b>bold</b>');
  });

  it('escapes the text, keeping the entities it spells', () => {
    expect(richTextHtml('Deals < 50% & more &nbsp;"true" > 3 &#39;x&#39;')).toBe(
      'Deals &lt; 50% &amp; more &nbsp;&quot;true&quot; &gt; 3 &#39;x&#39;',
    );
  });
});

@Component({
  imports: [RichText],
  template: '<lodb-rich-text [text]="text()" />',
})
class Host {
  readonly text = signal('<passive>Mana Charge:</passive> grants <stats>+1 mana</stats>');
}

describe('lodb-rich-text', () => {
  it('renders the rebuilt markup as elements, styled by ddragon-rich', async () => {
    const fixture = TestBed.createComponent(Host);
    await fixture.whenStable();
    const element = (fixture.nativeElement as HTMLElement).querySelector('lodb-rich-text');

    expect(element?.classList).toContain('ddragon-rich');
    expect(element?.querySelector('passive')?.textContent).toBe('Mana Charge:');
    expect(element?.querySelector('stats')?.textContent).toBe('+1 mana');
  });
});
