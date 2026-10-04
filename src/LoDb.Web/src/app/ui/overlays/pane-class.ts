import type { DialogSize } from './dialog-size';

/** The CDK pane classes of a dialog or sheet: the base pane class, then its size modifier. */
export function paneClass(pane: string, size: DialogSize | undefined): string | string[] {
  return size === undefined ? pane : [pane, `${pane}--${size}`];
}
