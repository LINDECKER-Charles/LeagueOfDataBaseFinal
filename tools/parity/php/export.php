<?php
declare(strict_types=1);

/*
 * Parity export of the legacy stack (L1.8): for one version and some languages, the
 * canonical projection that `catalog export` writes on the new stack, computed by the
 * legacy managers from what the storage volume already holds.
 *
 * Read-only by construction: every dataset, champion detail and chroma file is checked for
 * presence before a manager reads it, so no manager falls back to a fetch; image verdicts
 * come from manifestStatus(), which never ingests. Nothing under app/ is touched: the
 * script is copied into the php container and run there as www-data.
 *
 *   php -d memory_limit=1G /tmp/lodb-parity/php/export.php --version=16.19.1 \
 *       --langs=en_US,fr_FR --out=/tmp/lodb-parity-out
 */

namespace LoDbParity;

const USAGE = 'Usage: export.php --version=<x.y.z> --langs=<code>[,<code>] --out=<dir>'
    .' [--app=<dir>]';
const DEFAULT_APP_DIR = '/var/www/html';
const EXIT_USAGE = 2;
const EXIT_FAILURE = 1;

exit(main($argv));

/** @param list<string> $argv */
function main(array $argv): int
{
    $options = parseOptions($argv);
    if ($options === null) {
        fwrite(STDERR, USAGE.PHP_EOL);

        return EXIT_USAGE;
    }

    require_once $options['app'].'/vendor/autoload.php';
    foreach (glob(__DIR__.'/lib/*.php') ?: [] as $file) {
        require_once $file;
    }

    try {
        exportAll(LegacyCatalog::boot($options['app']), $options);
    } catch (NotWarmedException $exception) {
        fwrite(STDERR, $exception->getMessage().PHP_EOL);

        return EXIT_FAILURE;
    }

    return 0;
}

/** @param array{version: string, langs: list<string>, out: string, app: string} $options */
function exportAll(LegacyCatalog $catalog, array $options): void
{
    foreach ($options['langs'] as $lang) {
        $projection = (new Projection($catalog, $options['version'], $lang))->build();
        $directory  = $options['out'].'/'.$options['version'];
        if (!is_dir($directory) && !mkdir($directory, 0o775, true) && !is_dir($directory)) {
            throw new \RuntimeException('Cannot create '.$directory);
        }

        $json = json_encode($projection, JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE
            | JSON_UNESCAPED_SLASHES | JSON_PRESERVE_ZERO_FRACTION | JSON_THROW_ON_ERROR);
        file_put_contents($directory.'/'.$lang.'.json', $json."\n");
        fwrite(STDOUT, sprintf('exported %s/%s%s', $options['version'], $lang, PHP_EOL));
    }
}

/**
 * @param list<string> $argv
 * @return array{version: string, langs: list<string>, out: string, app: string}|null
 */
function parseOptions(array $argv): ?array
{
    $values = ['app' => DEFAULT_APP_DIR];
    foreach (array_slice($argv, 1) as $argument) {
        if (preg_match('/^--(version|langs|out|app)=(.+)$/', $argument, $match) !== 1) {
            return null;
        }
        $values[$match[1]] = $match[2];
    }

    if (!isset($values['version'], $values['langs'], $values['out'])) {
        return null;
    }

    return [
        'version' => $values['version'],
        'langs'   => array_values(array_filter(array_map('trim', explode(',', $values['langs'])))),
        'out'     => rtrim($values['out'], '/'),
        'app'     => rtrim($values['app'], '/'),
    ];
}
