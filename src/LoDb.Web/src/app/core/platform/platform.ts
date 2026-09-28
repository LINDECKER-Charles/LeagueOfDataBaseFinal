import { InjectionToken } from '@angular/core';
import type { PlatformService } from './platform-service';

/**
 * The implementation detected at startup. Injecting it before the detection initializer has
 * finished throws; startup code that needs it awaits `ActivePlatform.detect()` instead.
 */
export const PLATFORM = new InjectionToken<PlatformService>('PLATFORM');
