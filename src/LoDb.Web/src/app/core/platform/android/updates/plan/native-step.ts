/**
 * What the native shell must do (ADR 0008, level 2): `immediate` below the minimum version
 * of the client policy, whose API answers 426 until the shell is updated; `flexible` below
 * the latest one, downloaded in the background and applied on restart.
 */
export type NativeStep = 'none' | 'flexible' | 'immediate';
