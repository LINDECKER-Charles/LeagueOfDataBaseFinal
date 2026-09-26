/** Utility of a form control, spelled out whole for the Tailwind scanner. */
export type FieldClass = 'hx-input' | 'hx-select' | 'hx-check' | 'hx-switch';

/**
 * Picks the look of a native form control from what it is: a select gets the chevron, a
 * checkbox the tick box or, when asked, the toggle switch; every other control is a text
 * field. All of them are set at 16px so iOS never zooms on focus.
 */
export function fieldClass(tagName: string, type: string, asSwitch: boolean): FieldClass {
  if (tagName.toLowerCase() === 'select') {
    return 'hx-select';
  }
  if (type === 'checkbox') {
    return asSwitch ? 'hx-switch' : 'hx-check';
  }
  return 'hx-input';
}
