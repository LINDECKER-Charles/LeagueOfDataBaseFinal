import type { PlatformKind } from '../../platform/platform-kind';

/** What the browser tells about itself: its language and screen, and the platform. */
export interface AudienceDevice {
  readonly language: string;
  readonly screen: string;
  readonly platform: PlatformKind;
}
