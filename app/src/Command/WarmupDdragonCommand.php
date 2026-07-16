<?php
declare(strict_types=1);

namespace App\Command;

use App\Service\API\WarmableManagerInterface;
use App\Service\Client\VersionManager;
use Symfony\Component\Console\Attribute\AsCommand;
use Symfony\Component\Console\Command\Command;
use Symfony\Component\Console\Input\InputInterface;
use Symfony\Component\Console\Input\InputOption;
use Symfony\Component\Console\Output\OutputInterface;
use Symfony\Component\Console\Style\SymfonyStyle;
use Symfony\Component\DependencyInjection\Attribute\AutowireIterator;

/**
 * Pre-warms every DDragon manager's JSON + images into object storage so no
 * user-facing request ever pays the cold Go-fetch cost. Run it on deploy and
 * whenever a new game version ships (defaults to the latest version only).
 */
#[AsCommand(
    name: 'app:ddragon:warmup',
    description: 'Pré-charge JSON + images DDragon (tous les managers) dans le stockage objet.'
)]
final class WarmupDdragonCommand extends Command
{
    /**
     * @param iterable<WarmableManagerInterface> $managers
     */
    public function __construct(
        #[AutowireIterator('app.ddragon.manager')]
        private readonly iterable $managers,
        private readonly VersionManager $versions,
    ) {
        parent::__construct();
    }

    protected function configure(): void
    {
        $this
            ->addOption('limit', 'l', InputOption::VALUE_REQUIRED, 'Ne traiter que les N versions les plus récentes', '1')
            ->addOption('only', 'o', InputOption::VALUE_REQUIRED, 'Versions précises (CSV), ex: 16.14.1,16.13.1')
            ->addOption('langs', null, InputOption::VALUE_REQUIRED, 'Langues précises (CSV), ex: fr_FR,en_US')
            ->addOption('force', 'f', InputOption::VALUE_NONE, 'Réécrire les images même si déjà présentes')
        ;
    }

    protected function execute(InputInterface $input, OutputInterface $output): int
    {
        $io    = new SymfonyStyle($input, $output);
        $force = (bool) $input->getOption('force');

        $versions = $this->resolveVersions($input->getOption('only'), $input->getOption('limit'));
        $langs    = $this->resolveLangs($input->getOption('langs'));
        $managers = iterator_to_array($this->managers, false);

        if (!$versions || !$langs || !$managers) {
            $io->warning(sprintf('Rien à faire (managers=%d, versions=%d, langues=%d).', count($managers), count($versions), count($langs)));
            return Command::SUCCESS;
        }

        $io->title(sprintf('Warmup DDragon — %d manager(s) × %d version(s) × %d langue(s)%s',
            count($managers), count($versions), count($langs), $force ? ' [FORCE]' : ''
        ));

        $firstLang = reset($langs);
        $start = hrtime(true);
        $ok = 0; $err = 0;

        foreach ($managers as $manager) {
            $label = (new \ReflectionClass($manager))->getShortName();
            foreach ($versions as $v) {
                // JSON par (version, langue) ; images indépendantes de la langue
                // (manifest keyé par version) → un seul fetch avec la 1re langue.
                foreach ($langs as $lang) {
                    try {
                        $manager->getData($v, $lang);
                        $ok++;
                    } catch (\Throwable $e) {
                        $io->writeln(sprintf('<error>× %s data %s/%s : %s</error>', $label, $v, $lang, $e->getMessage()));
                        $err++;
                    }
                }
                try {
                    $count = count($manager->getImages($v, $firstLang, $force));
                    $io->writeln(sprintf('• %-16s %-10s : %d image(s)', $label, $v, $count));
                } catch (\Throwable $e) {
                    $io->writeln(sprintf('<error>× %s images %s : %s</error>', $label, $v, $e->getMessage()));
                    $err++;
                }
            }
        }

        $io->success(sprintf('Terminé en %.1fs. OK=%d, erreurs=%d.', (hrtime(true) - $start) / 1e9, $ok, $err));

        return $err === 0 ? Command::SUCCESS : Command::FAILURE;
    }

    /**
     * @return string[]
     */
    private function resolveVersions(mixed $only, mixed $limit): array
    {
        $versions = $this->versions->getVersions(); // plus récente d'abord
        if (is_string($only) && $only !== '') {
            $wanted = array_map('trim', explode(',', $only));
            return array_values(array_intersect($versions, $wanted));
        }

        return array_slice($versions, 0, max(1, (int) $limit));
    }

    /**
     * @return string[]
     */
    private function resolveLangs(mixed $langs): array
    {
        if (is_string($langs) && $langs !== '') {
            return array_values(array_map('trim', explode(',', $langs)));
        }
        $available = $this->versions->getLanguages();

        return $available ?: array_keys($this->versions->getLanguageLabels());
    }
}
