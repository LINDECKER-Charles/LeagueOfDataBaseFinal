import { Directive, ElementRef, computed, inject, input } from '@angular/core';
import { fieldClass } from './field-class';

type FieldElement = HTMLInputElement | HTMLSelectElement | HTMLTextAreaElement;

/**
 * Hextech look on a native form control, which keeps its label, validation and form
 * behaviour: `<input lodbField>`, `<select lodbField>`, `<input type="checkbox"
 * lodbField="switch">` for a toggle.
 */
@Directive({
  selector: 'input[lodbField], select[lodbField], textarea[lodbField]',
  host: { '[class]': 'look()' },
})
export class Field {
  /** `switch` turns a checkbox into a toggle; any other control ignores it. */
  readonly lodbField = input<'' | 'switch'>('');

  private readonly element = inject<ElementRef<FieldElement>>(ElementRef).nativeElement;

  protected readonly look = computed(() =>
    fieldClass(this.element.tagName, this.element.type, this.lodbField() === 'switch'),
  );
}
