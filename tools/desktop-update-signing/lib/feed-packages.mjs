// The packages a signed feed lists, checked against the files of a release folder: what the
// release uploads must be what the feed's checksums name, since the app accepts a package
// only when its size and SHA-256 match the signed feed.
import { createHash } from 'node:crypto';
import { createReadStream, statSync } from 'node:fs';
import { join } from 'node:path';
import { pipeline } from 'node:stream/promises';

async function sha256Of(path) {
  const hash = createHash('sha256');
  await pipeline(createReadStream(path), hash);
  return hash.digest('hex');
}

/** Throws on the first listed package missing from the folder or differing from the feed. */
export async function checkPackages({ assets, directory }) {
  for (const asset of assets) {
    const path = join(directory, asset.FileName);
    const size = statSync(path, { throwIfNoEntry: false })?.size;
    if (size === undefined) {
      throw new Error(`${asset.FileName} is listed by the feed but missing from ${directory}`);
    }
    if (size !== asset.Size) {
      throw new Error(`${asset.FileName} has ${size} bytes, the feed says ${asset.Size}`);
    }
    if ((await sha256Of(path)) !== asset.SHA256.toLowerCase()) {
      throw new Error(`${asset.FileName} does not match the SHA-256 of the feed`);
    }
  }
}
