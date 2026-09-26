import { ChangeDetectionStrategy, Component } from '@angular/core';
import { type ComponentFixture, TestBed } from '@angular/core/testing';
import { Accordion } from './accordion';
import { AccordionItem } from './accordion-item';

@Component({
  imports: [Accordion, AccordionItem],
  template: `
    <lodb-accordion>
      <lodb-accordion-item id="first" heading="Lore" [count]="2">lore body</lodb-accordion-item>
      <lodb-accordion-item id="second" heading="Tips" expanded>tips body</lodb-accordion-item>
    </lodb-accordion>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
class AccordionHost {}

describe('Accordion', () => {
  async function render(): Promise<ComponentFixture<AccordionHost>> {
    const fixture = TestBed.createComponent(AccordionHost);
    await fixture.whenStable();
    return fixture;
  }

  function parts(fixture: ComponentFixture<AccordionHost>, id: string) {
    const item = (fixture.nativeElement as HTMLElement).querySelector(`#${id}`);
    const button = item?.querySelector('button') as HTMLButtonElement;
    const body = item?.querySelector('[role=region]') as HTMLElement;
    return { button, body };
  }

  it('keeps a folded body in the DOM, hidden and labelled by its button', async () => {
    const { button, body } = parts(await render(), 'first');

    expect(button.getAttribute('aria-expanded')).toBe('false');
    expect(body.hidden).toBe(true);
    expect(body.textContent).toContain('lore body');
    expect(body.getAttribute('aria-labelledby')).toBe(button.id);
    expect(button.getAttribute('aria-controls')).toBe(body.id);
  });

  it('opens one item and closes the others', async () => {
    const fixture = await render();
    const first = parts(fixture, 'first');
    const second = parts(fixture, 'second');

    first.button.click();
    await fixture.whenStable();

    expect(first.button.getAttribute('aria-expanded')).toBe('true');
    expect(first.body.hidden).toBe(false);
    expect(second.button.getAttribute('aria-expanded')).toBe('false');
    expect(second.body.hidden).toBe(true);
  });
});
