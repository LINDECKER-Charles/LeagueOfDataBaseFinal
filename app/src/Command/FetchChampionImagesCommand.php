<?php
declare(strict_types=1);

namespace App\Command;

use App\Service\ChampionManager;
use App\Service\VersionManager;
use Symfony\Component\Console\Attribute\AsCommand;
use Symfony\Component\Console\Command\Command;
use Symfony\Component\Console\Input\InputInterface;
use Symfony\Component\Console\Input\InputOption;
use Symfony\Component\Console\Output\OutputInterface;
use Symfony\Component\Console\Style\SymfonyStyle;

#[AsCommand(
    name: 'app:champions:fetch-images',
    description: 'Télécharge les images des sorts pour toutes les versions × toutes les langues.'
)]
final class FetchChampionImagesCommand extends Command
{
    public function __construct(
        private readonly ChampionManager $champions,
        private readonly VersionManager $versions,
    ) {
        parent::__construct();
    }

    protected function configure(): void
    {
        $this->addOption('force', 'f', InputOption::VALUE_NONE, 'Réécrire même si le fichier existe');
    }

    protected function execute(InputInterface $input, OutputInterface $output): int
    {
        $io    = new SymfonyStyle($input, $output);
        $force = (bool) $input->getOption('force');

        $allVersions = $this->versions->getVersions();
        $allLangs    = $this->versions->getLanguages();
        if (empty($allLangs)) {
            $allLangs = array_keys($this->versions->getLanguageLabels()); // petit fallback
        }

        $io->title(sprintf('Champion images: %d versions × %d langues%s',
            count($allVersions), count($allLangs), $force ? ' [FORCE]' : ''
        ));

        foreach ($allVersions as $v) {
            foreach ($allLangs as $lang) {
                try {
                    $result = $this->champions->fetchChampionImages($v, $lang, $force);
                    $io->writeln(sprintf('• %s / %s : %d image(s)', $v, $lang, count($result)));
                } catch (\Throwable $e) {
                    $io->writeln(sprintf('<error>× %s / %s : %s</error>', $v, $lang, $e->getMessage()));
                }
            }
        }

        $io->success('Terminé.');
        return Command::SUCCESS;
    }
}
