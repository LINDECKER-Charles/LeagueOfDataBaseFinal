import { compileMessage } from '../../../src/LoDb.Web/i18n/lib/compile-message.mjs';

/**
 * Compiles a catalogue message as the web build does (`npm run i18n:build`, which compiles
 * every ICU message for the site's runtime), and throws on what that build would refuse: a
 * syntax error, or a construct the site does not render. Reporting it here names the message
 * before a build fails on it.
 */
export function compileIcu(message) {
  return compileMessage(message);
}
