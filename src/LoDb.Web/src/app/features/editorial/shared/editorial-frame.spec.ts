import { ChangeDetectionStrategy, Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { EditorialFrame } from './editorial-frame';

@Component({
  imports: [EditorialFrame],
  template: `
    <lodb-editorial-frame id="about" ambient eyebrow="About" heading="About us">
      <p>prose</p>
    </lodb-editorial-frame>
    <lodb-editorial-frame id="legal" headingSpacing="plate" eyebrow="Legal" heading="Privacy">
      <p>prose</p>
    </lodb-editorial-frame>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
class FramesHost {}

describe('lodb-editorial-frame', () => {
  function render(): HTMLElement {
    const fixture = TestBed.createComponent(FramesHost);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  it('lays the ambient backdrop under the column of an about page only', () => {
    const host = render();

    expect(host.querySelectorAll('#about lodb-backdrop')).toHaveLength(1);
    expect(host.querySelector('#legal lodb-backdrop')).toBeNull();
  });

  it('stands a title further from a date plate than from a lead', () => {
    const host = render();

    expect(host.querySelector('#about h1')?.classList).toContain('mb-5');
    expect(host.querySelector('#legal h1')?.classList).toContain('mb-6');
    expect(host.querySelector('#legal h1')?.classList).not.toContain('mb-5');
  });
});
