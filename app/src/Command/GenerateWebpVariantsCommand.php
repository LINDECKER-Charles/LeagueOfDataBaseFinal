<?php
declare(strict_types=1);

namespace App\Command;

use App\Service\Storage\ImageTranscoder;
use League\Flysystem\FilesystemOperator;
use Symfony\Component\Console\Attribute\AsCommand;
use Symfony\Component\Console\Command\Command;
use Symfony\Component\Console\Input\InputInterface;
use Symfony\Component\Console\Input\InputOption;
use Symfony\Component\Console\Output\OutputInterface;
use Symfony\Component\Console\Style\SymfonyStyle;

/**
 * Backfills the WebP sibling (blobs/<sha>.webp) for every image blob already in
 * object storage. New blobs get their WebP at ingestion ({@see \App\Service\Storage\BlobStore});
 * this command covers the ones stored before WebP existed. Idempotent.
 */
#[AsCommand(
    name: 'app:ddragon:webp',
    description: 'Génère les variantes WebP manquantes pour les images déjà stockées.'
)]
final class GenerateWebpVariantsCommand extends Command
{
    private const PREFIX = 'blobs';
    private const SOURCE_EXT = ['png', 'jpg', 'jpeg', 'gif'];

    public function __construct(
        private readonly FilesystemOperator $ddragonStorage,
        private readonly ImageTranscoder $transcoder,
    ) {
        parent::__construct();
    }

    protected function configure(): void
    {
        $this->addOption(
            'force',
            'f',
            InputOption::VALUE_NONE,
            'Réécrire même si le WebP existe déjà'
        );
    }

    protected function execute(InputInterface $input, OutputInterface $output): int
    {
        $io = new SymfonyStyle($input, $output);

        if (!$this->transcoder->isSupported()) {
            $io->error('Extension GD/WebP indisponible (imagewebp).');
            return Command::FAILURE;
        }

        ['done' => $done, 'skipped' => $skipped, 'failed' => $failed]
            = $this->backfillSiblings((bool) $input->getOption('force'));

        $io->success(sprintf(
            'WebP : %d générés, %d déjà présents, %d échecs.',
            $done,
            $skipped,
            $failed
        ));

        return $failed === 0 ? Command::SUCCESS : Command::FAILURE;
    }

    /** @return array{done: int, skipped: int, failed: int} */
    private function backfillSiblings(bool $force): array
    {
        $done = 0;
        $skipped = 0;
        $failed = 0;

        foreach ($this->convertibleBlobs() as $path => $webpKey) {
            if (!$force && $this->ddragonStorage->fileExists($webpKey)) {
                ++$skipped;
                continue;
            }
            if ($this->writeWebpSibling($path, $webpKey)) {
                ++$done;
                continue;
            }
            ++$failed;
        }

        return ['done' => $done, 'skipped' => $skipped, 'failed' => $failed];
    }

    /**
     * Stored blobs that can get a WebP sibling, paired with the key it goes to.
     *
     * @return iterable<string, string> source blob path => WebP sibling key
     */
    private function convertibleBlobs(): iterable
    {
        foreach ($this->ddragonStorage->listContents(self::PREFIX, false) as $item) {
            if (!$item->isFile()) {
                continue;
            }
            $webpKey = self::webpSiblingKey($item->path());
            if ($webpKey !== null) {
                yield $item->path() => $webpKey;
            }
        }
    }

    /** Null when the blob is not one of the raster sources we transcode. */
    private static function webpSiblingKey(string $path): ?string
    {
        $extension = strtolower(pathinfo($path, PATHINFO_EXTENSION));
        if (!in_array($extension, self::SOURCE_EXT, true)) {
            return null;
        }
        $webpKey = preg_replace('/\.[a-z0-9]+$/i', '.webp', $path);

        return $webpKey === null || $webpKey === $path ? null : $webpKey;
    }

    /** False when the source can't be transcoded — nothing is written then. */
    private function writeWebpSibling(string $source, string $webpKey): bool
    {
        $webp = $this->transcoder->toWebp($this->ddragonStorage->read($source));
        if ($webp === null) {
            return false;
        }
        $this->ddragonStorage->write($webpKey, $webp);

        return true;
    }
}
