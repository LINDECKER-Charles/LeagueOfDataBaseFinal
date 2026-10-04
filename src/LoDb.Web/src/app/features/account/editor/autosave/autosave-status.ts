/**
 * Where the automatic save of the profile stands: nothing to tell, saving, saved (which fades
 * back to idle), saved with something dropped, or failed (both stay on screen).
 */
export type AutosaveStatus = 'idle' | 'saving' | 'saved' | 'warned' | 'error';
