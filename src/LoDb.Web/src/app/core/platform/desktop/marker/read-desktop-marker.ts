import type { BridgeTransport } from '../bridge/bridge-transport';
import type { DesktopMarker } from './desktop-marker';

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null;
}

function transportOf(value: unknown): BridgeTransport | null {
  return isRecord(value) &&
    typeof value['send'] === 'function' &&
    typeof value['listen'] === 'function'
    ? (value as unknown as BridgeTransport)
    : null;
}

/**
 * The marker the host left in the page's globals, or null without a readable one: the same
 * test as the detection (`core/platform/detection`), plus the bridge, kept only when both
 * its functions are there.
 */
export function readDesktopMarker(globals: object | null): DesktopMarker | null {
  const marker = isRecord(globals)
    ? (globals as Record<string, unknown>)['__LODB_DESKTOP__']
    : null;
  if (!isRecord(marker) || typeof marker['version'] !== 'string') {
    return null;
  }
  return { version: marker['version'], bridge: transportOf(marker['bridge']) };
}
