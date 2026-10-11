import { randomUUID } from 'node:crypto';
import { copyFile, mkdir, readdir, rename, utimes } from 'node:fs/promises';
import { dirname, join } from 'node:path';
import { isHashedAsset } from '../hashed-asset';
import type { PublishedBuild } from './published-build';

// Where the hashed files live in the browser output (hashed-asset.ts): bundles at its root,
// media in a folder of their own.
const MEDIA_FOLDER = 'media';
// A copy lands here first, then moves into place whole: a client never reads a half-written
// file, even when two containers start at once or one stops mid-copy. A dot folder, which
// the static server never serves.
const INCOMING_FOLDER = '.incoming';

async function hashedFilesOf(browserFolder: string): Promise<string[]> {
  const media = await readdir(join(browserFolder, MEDIA_FOLDER)).catch(() => []);
  const candidates = [
    ...(await readdir(browserFolder)),
    ...media.map((name) => `${MEDIA_FOLDER}/${name}`),
  ];
  return candidates.filter(isHashedAsset);
}

// The date tells the deploy job's purge that a build still ships the file.
async function refresh(path: string, now: Date): Promise<boolean> {
  try {
    await utimes(path, now, now);
    return true;
  } catch (error) {
    if ((error as NodeJS.ErrnoException).code === 'ENOENT') {
      return false;
    }
    throw error;
  }
}

async function copyInto(source: string, target: string, sharedFolder: string): Promise<void> {
  const incoming = join(sharedFolder, INCOMING_FOLDER, randomUUID());
  await mkdir(dirname(incoming), { recursive: true });
  await copyFile(source, incoming);
  await mkdir(dirname(target), { recursive: true });
  // Same name, same bytes: a concurrent start that moved it first loses nothing.
  await rename(incoming, target);
}

/**
 * Publishes the hashed files of this image (bundles, media) into the folder every build
 * shares (`LODB_FRONT_BUILDS_DIR`, volume `front-builds`), so that a tab or a cached page of
 * this build still loads them once the next one is deployed. A file already there is never
 * rewritten (its name is its content); its date is refreshed, and the deploy job purges the
 * files that no build has shipped for a while.
 */
export async function publishFrontBuilds(
  browserFolder: string,
  sharedFolder: string,
  now: Date,
): Promise<PublishedBuild> {
  let copied = 0;
  let refreshed = 0;
  for (const file of await hashedFilesOf(browserFolder)) {
    const target = join(sharedFolder, file);
    if (await refresh(target, now)) {
      refreshed += 1;
    } else {
      await copyInto(join(browserFolder, file), target, sharedFolder);
      copied += 1;
    }
  }
  return { copied, refreshed };
}
