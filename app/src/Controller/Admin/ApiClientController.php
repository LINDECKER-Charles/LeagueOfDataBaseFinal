<?php
declare(strict_types=1);

namespace App\Controller\Admin;

use App\Entity\ApiKey;
use App\Entity\ApiPlan;
use App\Repository\ApiKeyRepository;
use App\Service\Audit\AuditAction;
use App\Service\Audit\AuditLogger;
use App\Service\Audit\AuditTarget;
use Doctrine\ORM\EntityManagerInterface;
use Psr\Log\LoggerInterface;
use Symfony\Component\HttpFoundation\Request;
use Symfony\Component\HttpFoundation\Response;
use Symfony\Component\Routing\Attribute\Route;

/**
 * Paid-API clients: fleet KPIs, per-key consumption, revocation and manual
 * credit grants (support gestures / refunds in kind). ROLE_ADMIN via the
 * /admin firewall; mutations are CSRF-checked POSTs.
 */
#[Route('/admin')]
final class ApiClientController extends AbstractAdminController
{
    private const CREDIT_MIN = 1;
    private const CREDIT_MAX = 1_000_000;
    private const TOP_CONSUMERS_DAYS = 30;
    private const TOP_CONSUMERS_LIMIT = 8;
    /** Neutral audit line — ids and amounts only, never account-identifying data. */
    private const LOG_CREDIT_GRANTED = 'admin.api_key.credits_granted';

    public function __construct(
        private readonly ApiKeyRepository $apiKeys,
        private readonly EntityManagerInterface $entityManager,
        private readonly LoggerInterface $logger,
        private readonly AuditLogger $audit,
    ) {}

    #[Route('/api-clients', name: 'admin_api_clients', methods: ['GET'])]
    public function index(Request $request): Response
    {
        $page = $this->pageParam($request);
        ['keys' => $keys, 'total' => $total] = $this->apiKeys->page($page, self::PER_PAGE);
        $monthStart = new \DateTimeImmutable('first day of this month midnight');

        return $this->render('admin/api_clients.html.twig', [
            'rows' => $this->rows($keys, $monthStart),
            'total' => $total,
            'page' => $page,
            'pages' => $this->pageCount($total),
            'kpis' => [
                'active' => $this->apiKeys->countActive(),
                'monthRequests' => $this->apiKeys->sumRequestsSince($monthStart),
                'credits' => $this->apiKeys->sumActiveCredits(),
                'byPlan' => $this->apiKeys->countActiveByPlan(),
            ],
            'topConsumers' => array_map(static fn (array $row): array => [
                'name' => sprintf('%s… — %s', $row['keyPrefix'], $row['username']),
                'count' => $row['requests'],
            ], $this->apiKeys->topConsumersSince(
                new \DateTimeImmutable(sprintf('-%d days midnight', self::TOP_CONSUMERS_DAYS)),
                self::TOP_CONSUMERS_LIMIT,
            )),
        ]);
    }

    #[Route('/api-clients/{id}/revoke', name: 'admin_api_client_revoke', methods: ['POST'])]
    public function revoke(ApiKey $key, Request $request): Response
    {
        if ($error = $this->csrfError($request, 'admin_api_client_revoke', 'admin_api_clients')) {
            return $error;
        }
        if (!$key->isActive()) {
            $this->addFlash('error', 'Cette clé est déjà révoquée.');

            return $this->backToList($request, 'admin_api_clients');
        }

        $key->revoke();
        $this->entityManager->flush();
        $this->audit->log(AuditAction::AdminApiClientRevoke, target: AuditTarget::of(AuditTarget::TYPE_API_CLIENT, $key->getId(), $key->getKeyPrefix() . '…'));
        $this->addFlash('success', sprintf('Clé %s… révoquée (effective sous ~60 s côté API).', $key->getKeyPrefix()));

        return $this->backToList($request, 'admin_api_clients');
    }

    #[Route('/api-clients/{id}/credit', name: 'admin_api_client_credit', methods: ['POST'])]
    public function credit(ApiKey $key, Request $request): Response
    {
        if ($error = $this->csrfError($request, 'admin_api_client_credit', 'admin_api_clients')) {
            return $error;
        }

        $requests = $request->request->getInt('requests');
        if ($requests < self::CREDIT_MIN || $requests > self::CREDIT_MAX) {
            $this->addFlash('error', sprintf('Nombre de requêtes invalide (%d à %s).', self::CREDIT_MIN, number_format(self::CREDIT_MAX, 0, '.', ' ')));

            return $this->backToList($request, 'admin_api_clients');
        }

        // Same atomic path as the Stripe credit packs (rate floor included).
        $this->apiKeys->addCredits($key, $requests, ApiPlan::RATE_CREDITS);
        $this->logger->info(self::LOG_CREDIT_GRANTED, ['api_key_id' => $key->getId(), 'requests' => $requests]);
        $this->audit->log(AuditAction::AdminApiClientCredit, target: AuditTarget::of(AuditTarget::TYPE_API_CLIENT, $key->getId(), $key->getKeyPrefix() . '…'), metadata: ['requests' => $requests]);
        $this->addFlash('success', sprintf('%s requêtes créditées sur la clé %s….', number_format($requests, 0, '.', ' '), $key->getKeyPrefix()));

        return $this->backToList($request, 'admin_api_clients');
    }

    /**
     * @param list<ApiKey> $keys
     * @return list<array{key: ApiKey, used: int}>
     */
    private function rows(array $keys, \DateTimeImmutable $monthStart): array
    {
        $ids = array_map(static fn (ApiKey $key): int => (int) $key->getId(), $keys);
        $usage = $this->apiKeys->usageByKeySince($ids, $monthStart);

        return array_map(static fn (ApiKey $key): array => [
            'key' => $key,
            'used' => $usage[(int) $key->getId()] ?? 0,
        ], $keys);
    }
}
