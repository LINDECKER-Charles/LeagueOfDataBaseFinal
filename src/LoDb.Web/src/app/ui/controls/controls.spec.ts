import { ChangeDetectionStrategy, Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Button } from './button';
import { Chip } from './chip';
import { Field } from './field';
import { fieldClass } from './field-class';

describe('fieldClass', () => {
  it.each([
    { tag: 'SELECT', type: 'select-one', asSwitch: false, look: 'hx-select' },
    { tag: 'INPUT', type: 'checkbox', asSwitch: false, look: 'hx-check' },
    { tag: 'INPUT', type: 'checkbox', asSwitch: true, look: 'hx-switch' },
    { tag: 'INPUT', type: 'search', asSwitch: false, look: 'hx-input' },
    { tag: 'INPUT', type: 'text', asSwitch: true, look: 'hx-input' },
    { tag: 'TEXTAREA', type: 'textarea', asSwitch: false, look: 'hx-input' },
  ])('styles a $tag of type $type (switch: $asSwitch) with $look', (row) => {
    expect(fieldClass(row.tag, row.type, row.asSwitch)).toBe(row.look);
  });
});

@Component({
  imports: [Button, Chip, Field],
  template: `
    <button id="default" class="w-full" lodbButton>Go</button>
    <a id="gold" href="/" lodbButton="gold">Go</a>
    <button id="danger" lodbButton="danger" lodbButtonSize="small">Delete</button>
    <span id="chip" lodbChip>tag</span>
    <span id="live" lodbChip="live">GET</span>
    <label>Name <input id="text" lodbField /></label>
    <label>On <input id="toggle" type="checkbox" lodbField="switch" /></label>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
class ControlsHost {}

describe('control directives', () => {
  function render(): HTMLElement {
    const fixture = TestBed.createComponent(ControlsHost);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  it.each([
    ['default', 'hx-btn'],
    ['gold', 'hx-btn-gold'],
    ['danger', 'hx-btn-danger'],
    ['chip', 'hx-chip'],
    ['live', 'hx-chip-hex'],
    ['text', 'hx-input'],
    ['toggle', 'hx-switch'],
  ])('gives #%s the %s look', (id, expected) => {
    expect(render().querySelector(`#${id}`)?.classList).toContain(expected);
  });

  it('keeps the classes the template sets itself', () => {
    expect(render().querySelector('#default')?.className).toBe('w-full hx-btn');
  });

  it('adds the compact size to the tone', () => {
    expect(render().querySelector('#danger')?.className).toBe('hx-btn-danger hx-btn-sm');
  });
});
