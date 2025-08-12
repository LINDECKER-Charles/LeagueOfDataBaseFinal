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
    name: 'app:champions:fetch-json',
    description: 'Récupère les JSON des sorts (champion.json) pour toutes les versions × toutes les langues.'
)]
final class FetchChampionJsonCommand extends Command
{
    public function __construct(
        private readonly ChampionManager $champions,
        private readonly VersionManager $versions,
    ) {
        parent::__construct();
    }

    protected function configure(): void
    {
        $this
            // Limiter aux N versions les plus récentes
            ->addOption('limit', 'l', InputOption::VALUE_REQUIRED, 'Limiter aux N dernières versions', null)
            // Cibler une liste précise de versions (CSV)
            ->addOption('only', 'o', InputOption::VALUE_REQUIRED, 'Versions à traiter (CSV), ex: 15.15.1,15.14.1', null)
            // Cibler une liste précise de langues (CSV). Par défaut: toutes les langues connues (API, puis fallback labels).
            ->addOption('langs', null, InputOption::VALUE_REQUIRED, 'Langues à traiter (CSV), ex: fr_FR,en_US', null)
        ;
    }

    protected function execute(InputInterface $input, OutputInterface $output): int
    {
        $io    = new SymfonyStyle($input, $output);
        $limit = $input->getOption('limit');
        $only  = $input->getOption('only');
        $langs = $input->getOption('langs');

        // Versions
        $versions = $this->versions->getVersions(); // la plus récente d'abord
        if (is_string($only) && $only !== '') {
            $wanted   = array_map('trim', explode(',', $only));
            $versions = array_values(array_intersect($versions, $wanted));
        } elseif ($limit !== null) {
            $n = max(0, (int) $limit);
            $versions = array_slice($versions, 0, $n);
        }

        // Langues
        if (is_string($langs) && $langs !== '') {
            $languages = array_map('trim', explode(',', $langs));
        } else {
            $languages = $this->versions->getLanguages();
            if (empty($languages)) {
                $languages = array_keys($this->versions->getLanguageLabels());
            }
        }

        if (!$versions || !$languages) {
            $io->warning(sprintf('Rien à faire (versions=%d, langues=%d).', count($versions), count($languages)));
            return Command::SUCCESS;
        }

        $io->title(sprintf('Fetch champion.json — %d version(s) × %d langue(s)', count($versions), count($languages)));

        $ok = 0; $err = 0;
        foreach ($versions as $v) {
            foreach ($languages as $lang) {
                try {
                    // getChampions écrit le JSON si absent (ou renvoie celui en cache local)
                    $json = $this->champions->getChampions($v, $lang);
                    $len  = strlen($json);
                    $io->writeln(sprintf('• %s / %s : %d octets', $v, $lang, $len));
                    $ok++;
                } catch (\Throwable $e) {
                    $io->writeln(sprintf('<error>× %s / %s : %s</error>', $v, $lang, $e->getMessage()));
                    $err++;
                }
            }
        }

        $io->success(sprintf('Terminé. OK=%d, erreurs=%d.', $ok, $err));
        return Command::SUCCESS;
    }
}
