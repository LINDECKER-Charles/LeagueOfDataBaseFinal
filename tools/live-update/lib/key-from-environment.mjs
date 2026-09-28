// The keys come from the environment only (a CI secret, a shell of the release host), never
// from a file of the repository: a key committed once stays in the history for good.

export const PRIVATE_KEY_VARIABLE = 'LODB_LIVE_UPDATE_PRIVATE_KEY';
export const PUBLIC_KEY_VARIABLE = 'LODB_LIVE_UPDATE_PUBLIC_KEY';
const PEM_BEGIN = '-----BEGIN ';

/**
 * The PEM held by the variable. Also takes it base64 encoded, the form some secret stores
 * require for multi-line values.
 */
export function keyFromEnvironment(variable, environment = process.env) {
  const value = environment[variable]?.trim();
  if (!value) {
    throw new Error(`${variable} is not set: export the PEM key, never commit it`);
  }
  if (value.startsWith(PEM_BEGIN)) {
    return value;
  }
  const decoded = Buffer.from(value, 'base64').toString('utf8').trim();
  if (!decoded.startsWith(PEM_BEGIN)) {
    throw new Error(`${variable} holds neither a PEM key nor a base64 encoded one`);
  }
  return decoded;
}
