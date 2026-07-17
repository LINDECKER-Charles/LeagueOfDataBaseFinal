<?php
declare(strict_types=1);

namespace App\Command;

use App\Service\Analytics\RollupService;
use Symfony\Component\Console\Attribute\AsCommand;
use Symfony\Component\Console\Command\Command;
use Symfony\Component\Console\Input\InputInterface;
use Symfony\Component\Console\Input\InputOption;
use Symfony\Component\Console\Output\OutputInterface;
use Symfony\Component\Console\Style\SymfonyStyle;

/**
 * Consolidates local NDJSON analytics days into durable MinIO aggregates. Run it
 * on a schedule (e.g. hourly with --include-today, or nightly) and/or on deploy;
 * the admin panel also exposes a manual trigger. Idempotent.
 */
#[AsCommand(
    name: 'app:analytics:rollup',
    description: 'Consolide les journées analytics locales (NDJSON) en agrégats MinIO immuables.'
)]
final class AnalyticsRollupCommand extends Command
{
    public function __construct(private readonly RollupService $rollup)
    {
        parent::__construct();
    }

    protected function configure(): void
    {
        $this
            ->addOption('include-today', 't', InputOption::VALUE_NONE, "Consolider aussi la journée courante (encore ouverte)")
            ->addOption('force', 'f', InputOption::VALUE_NONE, 'Réécrire les agrégats déjà présents')
            ->addOption('prune', 'p', InputOption::VALUE_NONE, 'Supprimer le NDJSON local des journées consolidées (hors aujourd\'hui)')
        ;
    }

    protected function execute(InputInterface $input, OutputInterface $output): int
    {
        $io = new SymfonyStyle($input, $output);

        $result = $this->rollup->rollup(
            includeToday: (bool) $input->getOption('include-today'),
            force: (bool) $input->getOption('force'),
            prune: (bool) $input->getOption('prune'),
        );

        $io->success(sprintf(
            'Rollup terminé : %d consolidée(s), %d ignorée(s), %d purgée(s).',
            count($result['rolled']),
            count($result['skipped']),
            count($result['pruned']),
        ));
        if ($result['rolled'] !== []) {
            $io->listing($result['rolled']);
        }

        return Command::SUCCESS;
    }
}
