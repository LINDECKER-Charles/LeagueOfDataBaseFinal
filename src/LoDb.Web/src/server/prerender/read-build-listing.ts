import { createHash } from 'node:crypto';
import { existsSync, readdirSync } from 'node:fs';
import { join } from 'node:path';
import { isHashedAsset } from '../hashed-asset';
import type { BuildListing } from './build-listing';

// Where the hashed files live in the browser output (hashed-asset.ts): bundles at its root,
// media in a folder of their own.
const MEDIA_FOLDER = 'media';
const NAME_SEPARATOR = '\n';
const BUILD_DIGEST = 'sha256';

/**
 * Reads the hashed files of the build in `browserFolder`, files of `public/` and templates
 * left out, and names the build by the digest of their sorted names. Read once at startup:
 * the list feeds `/_lodb/build-files`, and its digest marks every page of the pass
 * (`buildMark`), so both always name the same build.
 */
export function readBuildListing(browserFolder: string): BuildListing {
  const mediaFolder = join(browserFolder, MEDIA_FOLDER);
  const media = existsSync(mediaFolder) ? readdirSync(mediaFolder) : [];
  const files = [...readdirSync(browserFolder), ...media.map((name) => `${MEDIA_FOLDER}/${name}`)]
    .filter(isHashedAsset)
    .sort();
  const build = createHash(BUILD_DIGEST).update(files.join(NAME_SEPARATOR)).digest('hex');
  return { build, files };
}
