import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ddragonHtml } from './ddragon-html';
import { DdragonHtmlPipe } from './ddragon-html-pipe';

describe('ddragonHtml', () => {
  it("keeps Data Dragon's own vocabulary and plain emphasis, lower-cased", () => {
    const raw = 'Deals <magicDamage>80 magic damage</magicDamage>.<br><b>Passive:</b> <i>fast</i>';
    expect(ddragonHtml(raw)).toBe(
      'Deals <magicdamage>80 magic damage</magicdamage>.<br><b>Passive:</b> <i>fast</i>',
    );
  });

  it('drops every attribute, even of a tag it keeps', () => {
    expect(ddragonHtml('<span class="x" onclick="alert(1)">hit</span><br />')).toBe(
      '<span>hit</span><br>',
    );
  });

  it('removes unknown tags and comments but keeps their text', () => {
    const raw = '<script>alert(1)</script><font color="#f00">red</font><!-- note -->ok';
    expect(ddragonHtml(raw)).toBe('alert(1)redok');
  });

  it('removes a loading element whatever its case', () => {
    expect(ddragonHtml('a<IMG SRC=x onerror=alert(1)>b<iframe src="//x"></iframe>c')).toBe('abc');
  });

  it('escapes a stray bracket and a bare ampersand, keeping entities', () => {
    expect(ddragonHtml('5 < 6 > 4 & Q&amp;A &#8212; &nbsp;"')).toBe(
      '5 &lt; 6 &gt; 4 &amp; Q&amp;A &#8212; &nbsp;&quot;',
    );
  });

  it('reads an unclosed tag as text', () => {
    expect(ddragonHtml('a <b')).toBe('a &lt;b');
  });
});

@Component({
  imports: [DdragonHtmlPipe],
  template: `<div [innerHTML]="raw | ddragonHtml"></div>`,
})
class Host {
  readonly raw = '<physicalDamage>50</physicalDamage><img src=x onerror="alert(1)">';
}

describe('ddragonHtml pipe', () => {
  it('renders the kept tags through innerHTML, unsanitized, the rest gone', () => {
    const fixture = TestBed.createComponent(Host);
    fixture.detectChanges();
    const div = (fixture.nativeElement as HTMLElement).querySelector('div') as HTMLElement;
    expect(div.innerHTML).toBe('<physicaldamage>50</physicaldamage>');
  });
});
