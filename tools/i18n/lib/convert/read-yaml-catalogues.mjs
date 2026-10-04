import { readdirSync, readFileSync } from 'node:fs';
import { join } from 'node:path';
import { requireWebPackage } from '../require-web-package.mjs';

const yaml = requireWebPackage('js-yaml');
const CATALOGUE_FILE = /^([a-z]+)\.([A-Za-z_]+)\.yaml$/;

/**
 * Reads every `<domain>.<locale>.yaml` of a Symfony translations folder, sorted so the
 * conversion always writes in the same order. Symfony's own `+intl-icu` files and any other
 * name are ignored: none exists in this project.
 */
export function readYamlCatalogues(yamlDir) {
  return readdirSync(yamlDir)
    .map((file) => CATALOGUE_FILE.exec(file))
    .filter((match) => match !== null)
    .sort((left, right) => left[0].localeCompare(right[0]))
    .map(([file, domain, locale]) => ({
      domain,
      locale,
      tree: yaml.load(readFileSync(join(yamlDir, file), 'utf8'), { filename: file }),
    }));
}
