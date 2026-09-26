import { ChangeDetectionStrategy, Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Card } from './card';
import { Frame } from './frame';
import { Skeleton } from './skeleton';

@Component({
  imports: [Card, Frame, Skeleton],
  template: `
    <section id="plain" lodbFrame>plain</section>
    <section id="ornate" lodbFrame="ornate">ornate</section>
    <lodb-card id="card" eyebrow="Codex" heading="Ahri" frame="interactive">body</lodb-card>
    <lodb-skeleton id="tile" shape="tile" />
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
class SurfacesHost {}

describe('surfaces', () => {
  function render(): HTMLElement {
    const fixture = TestBed.createComponent(SurfacesHost);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  it.each([
    ['plain', ['hextech-frame']],
    ['ornate', ['hextech-frame', 'hx-corners']],
    ['card', ['block', 'p-5', 'hextech-frame', 'hextech-frame-hover']],
    ['tile', ['hx-sk', 'hx-sk-tile']],
  ])('gives #%s the classes %j', (id, expected) => {
    const classes = Array.from(render().querySelector(`#${id}`)?.classList ?? []);

    expect(classes).toEqual(expect.arrayContaining(expected));
  });

  it('renders the card eyebrow, heading and content', () => {
    const card = render().querySelector('#card');

    expect(card?.querySelector('.eyebrow')?.textContent).toBe('Codex');
    expect(card?.querySelector('h3')?.textContent).toBe('Ahri');
    expect(card?.textContent).toContain('body');
  });

  it('hides a skeleton from assistive technology', () => {
    expect(render().querySelector('#tile')?.getAttribute('aria-hidden')).toBe('true');
  });
});
