import { readFileSync, readdirSync } from 'node:fs';
import { join, relative, sep } from 'node:path';
import type { BuildFiles } from '../../app/core/http/build-files';
import { deepFreeze } from '../../app/core/http/deep-freeze';

const JSON_FILE = '.json';

function jsonFilesUnder(folder: string): string[] {
  return readdirSync(folder, { recursive: true, withFileTypes: true })
    .filter((entry) => entry.isFile() && entry.name.endsWith(JSON_FILE))
    .map((entry) => join(entry.parentPath, entry.name));
}

/**
 * Reads, parses and freezes every JSON file under `folders` of the browser build `root`, keyed
 * by its path relative to it with `/` separators (`i18n/fr.json`): the files a render would
 * otherwise request from the server itself. Once, at start-up: a missing folder or a corrupt
 * file stops the server rather than leaving pages without their texts.
 */
export function readBuildFiles(root: string, folders: readonly string[]): BuildFiles {
  const files = new Map<string, unknown>();
  for (const file of folders.flatMap((folder) => jsonFilesUnder(join(root, folder)))) {
    const path = relative(root, file).split(sep).join('/');
    files.set(path, deepFreeze(JSON.parse(readFileSync(file, 'utf8'))));
  }
  return files;
}
