import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { qrCode } from './qr-code';
import type { QrModules } from './qr-matrix';

// The quiet zone a reader needs around the symbol, in modules.
const QUIET_ZONE = 4;

interface QrShape {
  readonly viewBox: string;
  readonly side: number;
  readonly path: string;
}

function shapeOf(modules: QrModules | null): QrShape | null {
  if (modules === null) {
    return null;
  }
  const side = modules.length + QUIET_ZONE * 2;
  const path = modules
    .flatMap((row, y) =>
      row.map((dark, x) => (dark ? `M${x + QUIET_ZONE} ${y + QUIET_ZONE}h1v1h-1z` : '')),
    )
    .join('');
  return { viewBox: `0 0 ${side} ${side}`, side, path };
}

/**
 * The QR code of a text, drawn in SVG by the admin's own encoder: dark modules on a light
 * ground whatever the theme, as the readers expect. Nothing for a text too long to encode.
 */
@Component({
  selector: 'lodb-qr-image',
  template: `
    @if (shape(); as qr) {
      <svg
        [attr.viewBox]="qr.viewBox"
        class="block w-full"
        role="img"
        shape-rendering="crispEdges"
        [attr.aria-label]="label()"
      >
        <rect class="qr-light" [attr.width]="qr.side" [attr.height]="qr.side" />
        <path class="qr-dark" [attr.d]="qr.path" />
      </svg>
    }
  `,
  styles: `
    .qr-light {
      fill: var(--color-text);
    }
    .qr-dark {
      fill: var(--color-void);
    }
  `,
  host: { class: 'block' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class QrImage {
  readonly text = input.required<string>();
  /** The accessible name of the image, translated. */
  readonly label = input.required<string>();

  protected readonly shape = computed(() => shapeOf(qrCode(this.text())));
}
