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
        $this->addOption('force', 'f', InputOption::VALUE_NONE, 'Réécrire même si le WebP existe déjà');
    }

    protected function execute(InputInterface $input, OutputInterface $output): int
    {
        $io = new SymfonyStyle($input, $output);

        if (!$this->transcoder->isSupported()) {
            $io->error('Extension GD/WebP indisponible (imagewebp).');
            return Command::FAILURE;
        }

        $force = (bool) $input->getOption('force');
        $done = 0;
        $skipped = 0;
        $failed = 0;

        foreach ($this->ddragonStorage->listContents(self::PREFIX, false) as $item) {
            if (!$item->isFile()) {
                continue;
            }
            $path = $item->path();
            if (!in_array(strtolower(pathinfo($path, PATHINFO_EXTENSION)), self::SOURCE_EXT, true)) {
                continue;
            }
            $webpKey = preg_replace('/\.[a-z0-9]+$/i', '.webp', $path);
            if ($webpKey === null || $webpKey === $path) {
                continue;
            }
            if (!$force && $this->ddragonStorage->fileExists($webpKey)) {
                ++$skipped;
                continue;
            }
            $webp = $this->transcoder->toWebp($this->ddragonStorage->read($path));
            if ($webp === null) {
                ++$failed;
                continue;
            }
            $this->ddragonStorage->write($webpKey, $webp);
            ++$done;
        }

        $io->success(sprintf('WebP : %d générés, %d déjà présents, %d échecs.', $done, $skipped, $failed));

        return $failed === 0 ? Command::SUCCESS : Command::FAILURE;
    }
}
