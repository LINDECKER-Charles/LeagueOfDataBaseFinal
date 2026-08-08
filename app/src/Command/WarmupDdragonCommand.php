<?php
declare(strict_types=1);

namespace App\Command;

use App\Service\API\DatasetRef;
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
    /** Console I/O and --force are ambient for the whole run (set in initialize()). */
    private SymfonyStyle $io;
    private bool $force = false;

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
            ->addOption(
                'limit',
                'l',
                InputOption::VALUE_REQUIRED,
                'Ne traiter que les N versions les plus récentes',
                '1'
            )
            ->addOption(
                'only',
                'o',
                InputOption::VALUE_REQUIRED,
                'Versions précises (CSV), ex: 16.14.1,16.13.1'
            )
            ->addOption(
                'langs',
                null,
                InputOption::VALUE_REQUIRED,
                'Langues précises (CSV), ex: fr_FR,en_US'
            )
            ->addOption(
                'force',
                'f',
                InputOption::VALUE_NONE,
                'Réécrire les images même si déjà présentes'
            )
        ;
    }

    protected function initialize(InputInterface $input, OutputInterface $output): void
    {
        $this->io = new SymfonyStyle($input, $output);
        $this->force = (bool) $input->getOption('force');
    }

    protected function execute(InputInterface $input, OutputInterface $output): int
    {
        $versions = $this->resolveVersions($input->getOption('only'), $input->getOption('limit'));
        $langs    = $this->resolveLangs($input->getOption('langs'));
        $managers = iterator_to_array($this->managers, false);

        if (!$versions || !$langs || !$managers) {
            $this->io->warning(sprintf(
                'Rien à faire (managers=%d, versions=%d, langues=%d).',
                count($managers), count($versions), count($langs)
            ));

            return Command::SUCCESS;
        }

        $this->announce(count($managers), count($versions), count($langs));

        $start = hrtime(true);
        $tally = $this->warmAll($managers, $versions, $langs);

        $this->io->success(sprintf(
            'Terminé en %.1fs. OK=%d, erreurs=%d.',
            (hrtime(true) - $start) / 1e9,
            $tally['ok'],
            $tally['errors']
        ));

        return $tally['errors'] === 0 ? Command::SUCCESS : Command::FAILURE;
    }

    private function announce(int $managers, int $versions, int $langs): void
    {
        $this->io->title(sprintf('Warmup DDragon — %d manager(s) × %d version(s) × %d langue(s)%s',
            $managers, $versions, $langs, $this->force ? ' [FORCE]' : ''
        ));
    }

    /**
     * @param list<WarmableManagerInterface> $managers
     * @param list<string>                   $versions
     * @param list<string>                   $langs
     * @return array{ok: int, errors: int}
     */
    private function warmAll(array $managers, array $versions, array $langs): array
    {
        // Images are language-independent (manifest keyed by version) → warming
        // them once with the first language covers every locale.
        $firstLang = (string) reset($langs);
        $ok        = 0;
        $errors    = 0;

        foreach ($managers as $manager) {
            foreach ($versions as $version) {
                $dataErrors = $this->warmData($manager, $version, $langs);
                // Every (version, lang) pair either warms or reports an error.
                $ok += count($langs) - $dataErrors;
                $errors += $dataErrors
                    + $this->warmImages($manager, new DatasetRef($version, $firstLang));
            }
        }

        return ['ok' => $ok, 'errors' => $errors];
    }

    /**
     * JSON warmup, one fetch per (version, lang).
     *
     * @param list<string> $langs
     * @return int number of failed fetches
     */
    private function warmData(
        WarmableManagerInterface $manager,
        string $version,
        array $langs,
    ): int {
        $errors = 0;

        foreach ($langs as $lang) {
            try {
                $manager->getData($version, $lang);
            } catch (\Throwable $e) {
                $this->io->writeln(sprintf(
                    '<error>× %s data %s/%s : %s</error>',
                    $this->label($manager),
                    $version,
                    $lang,
                    $e->getMessage()
                ));
                ++$errors;
            }
        }

        return $errors;
    }

    /**
     * Image warmup for one version.
     *
     * @return int 1 when the fetch failed, 0 otherwise
     */
    private function warmImages(WarmableManagerInterface $manager, DatasetRef $dataset): int
    {
        $label = $this->label($manager);

        try {
            $count = count($manager->getImages($dataset, $this->force));
            $this->io->writeln(sprintf(
                '• %-16s %-10s : %d image(s)',
                $label,
                $dataset->version,
                $count
            ));
        } catch (\Throwable $e) {
            $this->io->writeln(sprintf(
                '<error>× %s images %s : %s</error>',
                $label,
                $dataset->version,
                $e->getMessage()
            ));

            return 1;
        }

        return 0;
    }

    private function label(WarmableManagerInterface $manager): string
    {
        return (new \ReflectionClass($manager))->getShortName();
    }

    /**
     * @return string[]
     */
    private function resolveVersions(mixed $only, mixed $limit): array
    {
        $versions = $this->versions->getVersions(); // newest first
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
