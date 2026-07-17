<?php
declare(strict_types=1);

namespace App\Service\Donation;

use App\Entity\Donation;
use App\Repository\DonationRepository;
use App\Repository\UserRepository;
use Doctrine\ORM\EntityManagerInterface;
use Psr\Log\LoggerInterface;

/**
 * Persists completed donations from the Stripe webhook. Logs carry the session
 * id, amounts and internal ids only — never the donor's identity. A donation
 * whose client_reference_id no longer matches an account is still recorded,
 * just unlinked (the money exists either way).
 */
final class DonationRecorder implements DonationLedger
{
    public function __construct(
        private readonly DonationRepository $donations,
        private readonly UserRepository $users,
        private readonly EntityManagerInterface $entityManager,
        private readonly LoggerInterface $logger,
    ) {}

    public function record(string $sessionId, int $amountCents, string $currency, ?int $userId): void
    {
        if ($sessionId === '') {
            $this->logger->warning('stripe.donation.session_without_id');

            return;
        }
        if ($this->donations->existsBySessionId($sessionId)) {
            // Redelivered event: already accounted for, acknowledge silently.
            return;
        }

        $user = $userId === null ? null : $this->users->find($userId);
        $this->entityManager->persist(new Donation($sessionId, $amountCents, $currency, $user));
        $user?->setIsSupporter(true);
        $this->entityManager->flush();

        $this->logger->info('stripe.donation.recorded', [
            'session' => $sessionId,
            'amount_cents' => $amountCents,
            'currency' => $currency,
            'linked' => $user !== null,
        ]);
    }
}
