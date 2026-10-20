// A certificate that no authority signed, for the stub's page store over TLS: nginx must refuse
// to read it. Node signs but writes no certificate, and the image has no openssl: the few DER
// structures of an X.509 v3 certificate (RFC 5280) are written here.
import { generateKeyPairSync, sign } from 'node:crypto';

const DAY_MS = 24 * 60 * 60 * 1000;
// DER lengths: one byte below 128, else 0x80 + the count of the bytes that follow.
const SHORT_FORM_LIMIT = 0x80;
const BYTE_VALUES = 256;
const TAG = { integer: 0x02, bitString: 0x03, oid: 0x06, utf8: 0x0c, utcTime: 0x17,
  sequence: 0x30, set: 0x31, explicitVersion: 0xa0 };
// ecdsa-with-SHA256 (1.2.840.10045.4.3.2) and commonName (2.5.4.3), DER-encoded.
const ECDSA_WITH_SHA256 = '2a8648ce3d040302';
const COMMON_NAME = '550403';
const VERSION_3 = 2;
const SERIAL = 1;

function lengthOf(size) {
  if (size < SHORT_FORM_LIMIT) {
    return Buffer.from([size]);
  }
  const bytes = [];
  for (let rest = size; rest > 0; rest = Math.floor(rest / BYTE_VALUES)) {
    bytes.unshift(rest % BYTE_VALUES);
  }
  return Buffer.from([SHORT_FORM_LIMIT | bytes.length, ...bytes]);
}

function element(tag, ...parts) {
  const body = Buffer.concat(parts);
  return Buffer.concat([Buffer.from([tag]), lengthOf(body.length), body]);
}

// UTCTime, YYMMDDHHMMSSZ.
function timeOf(date) {
  const digits = date.toISOString().replace(/[-:T]/g, '').slice(2, 14);
  return element(TAG.utcTime, Buffer.from(`${digits}Z`));
}

function toBeSigned(name, publicKey, now) {
  const algorithm = element(TAG.sequence, element(TAG.oid, Buffer.from(ECDSA_WITH_SHA256, 'hex')));
  const commonName = element(TAG.sequence, element(TAG.oid, Buffer.from(COMMON_NAME, 'hex')),
    element(TAG.utf8, Buffer.from(name)));
  const subject = element(TAG.sequence, element(TAG.set, commonName));
  const validity = element(TAG.sequence, timeOf(new Date(now - DAY_MS)),
    timeOf(new Date(now + DAY_MS)));
  return { algorithm, body: element(TAG.sequence,
    element(TAG.explicitVersion, element(TAG.integer, Buffer.from([VERSION_3]))),
    element(TAG.integer, Buffer.from([SERIAL])), algorithm, subject, validity, subject,
    publicKey.export({ type: 'spki', format: 'der' })) };
}

function pemOf(der) {
  const lines = der.toString('base64').match(/.{1,64}/g).join('\n');
  return `-----BEGIN CERTIFICATE-----\n${lines}\n-----END CERTIFICATE-----\n`;
}

/**
 * A fresh key and a certificate for `name` that the key signs itself, valid from yesterday to
 * tomorrow; both in PEM.
 */
export function selfSignedCertificate(name) {
  const { privateKey, publicKey } = generateKeyPairSync('ec', { namedCurve: 'prime256v1' });
  const { algorithm, body } = toBeSigned(name, publicKey, Date.now());
  const signature = element(TAG.bitString, Buffer.from([0]), sign('sha256', body, privateKey));
  return {
    certificate: pemOf(element(TAG.sequence, body, algorithm, signature)),
    key: privateKey.export({ type: 'pkcs8', format: 'pem' }),
  };
}
