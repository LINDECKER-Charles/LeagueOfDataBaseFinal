import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { DdragonHtmlPipe } from './ddragon-html-pipe';

@Component({
  imports: [DdragonHtmlPipe],
  template: `<div [innerHTML]="raw | ddragonHtml"></div>`,
})
class Host {
  readonly raw =
    '<physicalDamage>50</physicalDamage><img src=x onerror="alert(1)">' +
    "<font color='#48C4B7'>adaptive</font>";
}

describe('ddragonHtml pipe', () => {
  it('renders the kept tags through innerHTML, unsanitized, the rest gone', () => {
    const fixture = TestBed.createComponent(Host);
    fixture.detectChanges();
    const div = (fixture.nativeElement as HTMLElement).querySelector('div') as HTMLElement;
    expect(div.innerHTML).toBe(
      '<physicaldamage>50</physicaldamage><font color="#48C4B7">adaptive</font>',
    );
  });
});
