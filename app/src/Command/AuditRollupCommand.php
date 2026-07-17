<?php
declare(strict_types=1);

namespace App\Command;

use App\Service\Audit\AuditRollupService;
use Symfony\Component\Console\Attribute\AsCommand;
use Symfony\Component\Console\Command\Command;
use Symfony\Component\Console\Input\InputInterface;
use Symfony\Component\Console\Input\InputOption;
use Symfony\Component\Console\Output\OutputInterface;
use Symfony\Component\Console\Style\SymfonyStyle;

/**
 * Archives closed local audit days into MinIO and enforces the CNIL retention
 * ceiling. Run on a schedule alongside {@see AnalyticsRollupCommand} (nightly
 * with --prune --enforce-retention); the admin panel also exposes a manual,
 * period-scoped purge. Idempotent.
 */
#[AsCommand(
    name: 'app:audit:rollup',
    description: 'Archive les journaux d\'audit locaux vers MinIO et applique la rétention CNIL (6 mois).'
)]
final class AuditRollupCommand extends Command
{
    public function __construct(private readonly AuditRollupService $rollup)
    {
        parent::__construct();
    }

    protected function configure(): void
    {
        $this
            ->addOption('prune', 'p', InputOption::VALUE_NONE, 'Supprimer le NDJSON local des journées archivées')
            ->addOption('enforce-retention', 'r', InputOption::VALUE_NONE, 'Supprimer les journées au-delà de la rétention CNIL (6 mois)')
        ;
    }

    protected function execute(InputInterface $input, OutputInterface $output): int
    {
        $io = new SymfonyStyle($input, $output);

        $result = $this->rollup->rollup(prune: (bool) $input->getOption('prune'));
        $io->success(sprintf(
            'Archivage terminé : %d journée(s) archivée(s), %d purgée(s) en local.',
            count($result['archived']),
            count($result['pruned']),
        ));

        if ($input->getOption('enforce-retention')) {
            $retention = $this->rollup->enforceRetention();
            $io->success(sprintf('Rétention : %d journée(s) supprimée(s) au-delà de 6 mois.', count($retention['deleted'])));
        }

        return Command::SUCCESS;
    }
}
