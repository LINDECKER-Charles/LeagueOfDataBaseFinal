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

  it('keeps the plain structure Data Dragon writes, lists and small print included', () => {
    expect(richTextHtml('<ol><li>1</li></ol><small>x<sub>2</sub><sup>3</sup></small>')).toBe(
      '<ol><li>1</li></ol><small>x<sub>2</sub><sup>3</sup></small>',
    );
  });

  it("keeps a <font>'s hex colour, the hint Riot gives a keyword, and nothing else", () => {
    expect(richTextHtml("<font color='#48C4B7'>adaptive damage</font>")).toBe(
      '<font color="#48C4B7">adaptive damage</font>',
    );
    expect(richTextHtml('<FONT size=2 COLOR=#ff0000>Soul</FONT>')).toBe(
      '<font color="#ff0000">Soul</font>',
    );
  });

  it('writes a bare <font> when its colour is anything but a hex colour', () => {
    expect(richTextHtml('<font color="red">a</font>')).toBe('<font>a</font>');
    expect(richTextHtml('<font color="#48C4B7;background:url(x)">b</font>')).toBe('<font>b</font>');
    expect(richTextHtml('<font color="#48C4B7" onclick="alert(1)">c</font>')).toBe(
      '<font color="#48C4B7">c</font>',
    );
    expect(richTextHtml('<font onclick="alert(1)" style="color:red">d</font>')).toBe(
      '<font>d</font>',
    );
  });

  it('keeps a link as its styled text, without the link', () => {
    expect(richTextHtml('<a href="javascript:alert(1)">link</a>')).toBe('<a>link</a>');
  });

  it('drops any other tag and any comment, keeping the text', () => {
    expect(
      richTextHtml('<script>alert(1)</script><!-- note -->ok<iframe src="//x"></iframe>'),
    ).toBe('alert(1)ok');
  });

  it('never lets a script, a handler or a URL through', () => {
    const html = richTextHtml(
      '<script>alert(1)</script><IMG SRC=x onerror=alert(1)><b onclick="alert(1)">bold</b>',
    );

    expect(html).toBe('alert(1)<b>bold</b>');
  });

  it('escapes the text, keeping the entities it spells', () => {
    expect(richTextHtml('Deals < 50% & more &nbsp;"true" > 3 &#39;x&#39;')).toBe(
      'Deals &lt; 50% &amp; more &nbsp;&quot;true&quot; &gt; 3 &#39;x&#39;',
    );
  });

  it('reads an unclosed tag as text', () => {
    expect(richTextHtml('a <b')).toBe('a &lt;b');
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
