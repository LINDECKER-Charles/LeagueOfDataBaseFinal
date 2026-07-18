<?php
declare(strict_types=1);

namespace App\Controller;

use App\Entity\ApiKey;
use App\Entity\ApiPlan;
use App\Entity\User;
use App\Repository\ApiKeyRepository;
use App\Service\Audit\AuditAction;
use App\Service\Audit\AuditLogger;
use App\Service\Audit\AuditTarget;
use App\Service\PublicApi\ApiCheckout;
use App\Service\PublicApi\ApiCreditPack;
use App\Service\PublicApi\ApiKeyIssuer;
use App\Service\Client\ClientManager;
use App\Service\Client\PageContextResolver;
use App\Service\Client\VersionManager;
use Doctrine\ORM\EntityManagerInterface;
use Symfony\Component\HttpFoundation\RedirectResponse;
use Symfony\Component\HttpFoundation\Request;
use Symfony\Component\HttpFoundation\RequestStack;
use Symfony\Component\HttpFoundation\Response;
use Symfony\Component\Routing\Attribute\Route;
use Symfony\Contracts\Translation\TranslatorInterface;

/**
 * Developer portal under /profile (ROLE_USER via access_control): key
 * lifecycle (create / regenerate / revoke, single active key v1) and the
 * consumption dashboard fed by go-api's api_usage metering. The raw key
 * transits through a dedicated flash slot consumed HERE, before rendering,
 * so the global toaster never sees it.
 */
final class ApiKeyController extends AbstractResourceController
{
    private const CSRF_TOKEN_ID = 'submit';
    private const RAW_KEY_FLASH = 'api_key_plain';
    private const USAGE_WINDOW_DAYS = 30;
    private const CHECKOUT_STATUSES = [
        ApiCheckout::STATUS_PACK_SUCCESS,
        ApiCheckout::STATUS_PLAN_SUCCESS,
        ApiCheckout::STATUS_CANCELLED,
    ];

    public function __construct(
        VersionManager $versionManager,
        ClientManager $clientManager,
        PageContextResolver $pageContext,
        RequestStack $requestStack,
        private readonly ApiKeyRepository $apiKeys,
        private readonly ApiKeyIssuer $issuer,
        private readonly ApiCheckout $checkout,
        private readonly EntityManagerInterface $entityManager,
        private readonly TranslatorInterface $translator,
        private readonly AuditLogger $audit,
    ) {
        parent::__construct($versionManager, $clientManager, $pageContext, $requestStack);
    }

    #[Route('/profile/api', name: 'app_api_portal', methods: ['GET'])]
    public function portal(Request $request): Response
    {
        $key = $this->apiKeys->findActiveByUser($this->currentUser());

        return $this->render('api/portal.html.twig', [
            'client' => $this->clientData(),
            'apiKey' => $key,
            'plainKey' => $this->consumePlainKey(),
            'checkoutStatus' => $this->checkoutStatus($request),
            'usedThisMonth' => $key === null ? 0 : $this->apiKeys->sumRequestsForMonth($key, new \DateTimeImmutable()),
            'dailyUsage' => $key === null ? [] : $this->apiKeys->recentDailyUsage($key, self::USAGE_WINDOW_DAYS),
            'packs' => ApiCreditPack::cases(),
            'plans' => ApiPlan::subscriptions(),
            'billingConfigured' => $this->checkout->isConfigured(),
        ]);
    }

    #[Route('/profile/api/create', name: 'app_api_key_create', methods: ['POST'])]
    public function create(Request $request): RedirectResponse
    {
        if (!$this->isCsrfTokenValid(self::CSRF_TOKEN_ID, (string) $request->request->get('_token'))) {
            return $this->backToPortalWithError('portal.flash.csrf');
        }

        if (($gate = $this->requireVerifiedEmail()) !== null) {
            return $gate;
        }

        $user = $this->currentUser();
        if ($this->apiKeys->findActiveByUser($user) !== null) {
            return $this->backToPortalWithError('portal.flash.key_exists');
        }

        $issued = $this->issuer->issue($user, (string) $request->request->get('name', ''));
        $this->entityManager->persist($issued->key);
        $this->entityManager->flush();
        $this->audit->log(AuditAction::ApiKeyCreate, target: AuditTarget::of(AuditTarget::TYPE_API_KEY, $issued->key->getId(), $issued->key->getKeyPrefix() . '…'));

        return $this->backToPortalWithRawKey($issued->rawKey, 'portal.flash.created');
    }

    #[Route('/profile/api/regenerate', name: 'app_api_key_regenerate', methods: ['POST'])]
    public function regenerate(Request $request): RedirectResponse
    {
        if (!$this->isCsrfTokenValid(self::CSRF_TOKEN_ID, (string) $request->request->get('_token'))) {
            return $this->backToPortalWithError('portal.flash.csrf');
        }

        $key = $this->apiKeys->findActiveByUser($this->currentUser());
        if ($key === null) {
            return $this->backToPortalWithError('portal.flash.no_key');
        }

        // Revocation of the old key + creation of the new one flush atomically;
        // then the metered days follow the lineage so the monthly quota cannot
        // be reset by rotating the secret.
        $issued = $this->issuer->regenerate($key);
        $this->entityManager->persist($issued->key);
        $this->entityManager->flush();
        $this->apiKeys->transferUsage($key, $issued->key);
        $this->audit->log(AuditAction::ApiKeyRegenerate, target: AuditTarget::of(AuditTarget::TYPE_API_KEY, $issued->key->getId(), $issued->key->getKeyPrefix() . '…'));

        return $this->backToPortalWithRawKey($issued->rawKey, 'portal.flash.regenerated');
    }

    #[Route('/profile/api/revoke', name: 'app_api_key_revoke', methods: ['POST'])]
    public function revoke(Request $request): RedirectResponse
    {
        if (!$this->isCsrfTokenValid(self::CSRF_TOKEN_ID, (string) $request->request->get('_token'))) {
            return $this->backToPortalWithError('portal.flash.csrf');
        }

        $key = $this->apiKeys->findActiveByUser($this->currentUser());
        if ($key === null) {
            return $this->backToPortalWithError('portal.flash.no_key');
        }

        $key->revoke();
        $this->entityManager->flush();
        $this->audit->log(AuditAction::ApiKeyRevoke, target: AuditTarget::of(AuditTarget::TYPE_API_KEY, $key->getId(), $key->getKeyPrefix() . '…'));
        $this->addFlash('success', $this->translator->trans('portal.flash.revoked', [], 'api'));

        return $this->redirectToRoute('app_api_portal', status: Response::HTTP_SEE_OTHER);
    }

    private function currentUser(): User
    {
        $user = $this->getUser();
        if (!$user instanceof User) {
            // access_control guarantees ROLE_USER; this guards the admin firewall identity.
            throw $this->createAccessDeniedException();
        }

        return $user;
    }

    /**
     * Minting an API key is reserved to confirmed accounts — same anti-abuse gate
     * as public build creation, since a key unlocks the paid public API. Returns a
     * redirect to bounce unverified users, or null to let the caller proceed.
     * Regeneration stays ungated on purpose: it only rotates an already-issued key
     * (e.g. a Stripe auto-provisioned one), never mints a first key.
     */
    private function requireVerifiedEmail(): ?RedirectResponse
    {
        if ($this->currentUser()->isVerified()) {
            return null;
        }

        $this->addFlash('warning', $this->translator->trans('auth.verify.gate_api'));

        return $this->redirectToRoute('app_api_portal', status: Response::HTTP_SEE_OTHER);
    }

    /** One-shot read of the freshly issued raw key — never reaches app.flashes. */
    private function consumePlainKey(): ?string
    {
        $values = $this->requestStack->getSession()->getFlashBag()->get(self::RAW_KEY_FLASH);
        $plain = $values[0] ?? null;

        return \is_string($plain) ? $plain : null;
    }

    /** Stripe return banner — allowlisted so the query param cannot inject arbitrary keys. */
    private function checkoutStatus(Request $request): ?string
    {
        $status = (string) $request->query->get('status', '');

        return \in_array($status, self::CHECKOUT_STATUSES, true) ? $status : null;
    }

    private function backToPortalWithRawKey(string $rawKey, string $successKey): RedirectResponse
    {
        $this->requestStack->getSession()->getFlashBag()->set(self::RAW_KEY_FLASH, [$rawKey]);
        $this->addFlash('success', $this->translator->trans($successKey, [], 'api'));

        return $this->redirectToRoute('app_api_portal', status: Response::HTTP_SEE_OTHER);
    }

    private function backToPortalWithError(string $messageKey): RedirectResponse
    {
        $this->addFlash('error', $this->translator->trans($messageKey, [], 'api'));

        return $this->redirectToRoute('app_api_portal', status: Response::HTTP_SEE_OTHER);
    }
}
