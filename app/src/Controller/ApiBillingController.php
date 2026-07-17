<?php
declare(strict_types=1);

namespace App\Controller;

use App\Entity\ApiKey;
use App\Entity\ApiPlan;
use App\Entity\User;
use App\Repository\ApiKeyRepository;
use App\Service\PublicApi\ApiCheckout;
use App\Service\PublicApi\ApiCreditPack;
use App\Service\Client\ClientManager;
use App\Service\Client\PageContextResolver;
use App\Service\Client\VersionManager;
use Psr\Log\LoggerInterface;
use Stripe\Exception\ApiErrorException;
use Symfony\Component\HttpFoundation\RedirectResponse;
use Symfony\Component\HttpFoundation\Request;
use Symfony\Component\HttpFoundation\RequestStack;
use Symfony\Component\HttpFoundation\Response;
use Symfony\Component\Routing\Attribute\Route;
use Symfony\Contracts\Translation\TranslatorInterface;

/**
 * Stripe checkout redirects of the API billing (packs + subscriptions),
 * separated from the key lifecycle controller on purpose (payments vs
 * credentials). Both routes 303 to Stripe's hosted page; entitlements are
 * applied by the webhook, never on the return URL.
 */
final class ApiBillingController extends AbstractResourceController
{
    private const CSRF_TOKEN_ID = 'submit';

    public function __construct(
        VersionManager $versionManager,
        ClientManager $clientManager,
        PageContextResolver $pageContext,
        RequestStack $requestStack,
        private readonly ApiKeyRepository $apiKeys,
        private readonly ApiCheckout $checkout,
        private readonly TranslatorInterface $translator,
        private readonly LoggerInterface $logger,
    ) {
        parent::__construct($versionManager, $clientManager, $pageContext, $requestStack);
    }

    #[Route('/profile/api/checkout/pack', name: 'app_api_checkout_pack', methods: ['POST'])]
    public function pack(Request $request): RedirectResponse
    {
        $key = $this->guardedActiveKey($request);
        if ($key instanceof RedirectResponse) {
            return $key;
        }

        $pack = ApiCreditPack::tryFrom((string) $request->request->get('pack'));
        if ($pack === null) {
            return $this->backToPortalWithError('portal.flash.invalid_offer');
        }

        return $this->redirectToStripe(fn (): string => $this->checkout->packSession($key->getUser(), $pack));
    }

    #[Route('/profile/api/checkout/plan', name: 'app_api_checkout_plan', methods: ['POST'])]
    public function plan(Request $request): RedirectResponse
    {
        $key = $this->guardedActiveKey($request);
        if ($key instanceof RedirectResponse) {
            return $key;
        }

        $plan = ApiPlan::tryFrom((string) $request->request->get('plan'));
        if ($plan === null || !$plan->isSubscription()) {
            return $this->backToPortalWithError('portal.flash.invalid_offer');
        }
        if ($key->hasSubscription()) {
            // One live subscription per key (v1): switching = cancel, then subscribe again.
            return $this->backToPortalWithError('portal.flash.already_subscribed');
        }

        return $this->redirectToStripe(fn (): string => $this->checkout->planSession($key->getUser(), $plan));
    }

    /** Shared gate: CSRF, configured gateway, existing active key — or the error redirect. */
    private function guardedActiveKey(Request $request): ApiKey|RedirectResponse
    {
        if (!$this->isCsrfTokenValid(self::CSRF_TOKEN_ID, (string) $request->request->get('_token'))) {
            return $this->backToPortalWithError('portal.flash.csrf');
        }
        if (!$this->checkout->isConfigured()) {
            return $this->backToPortalWithError('portal.flash.stripe_unavailable');
        }

        $user = $this->getUser();
        if (!$user instanceof User) {
            throw $this->createAccessDeniedException();
        }

        // Purchases require a key to land on; the webhook stays defensive anyway.
        return $this->apiKeys->findActiveByUser($user)
            ?? $this->backToPortalWithError('portal.flash.need_key');
    }

    /** @param callable(): string $createSession */
    private function redirectToStripe(callable $createSession): RedirectResponse
    {
        try {
            return new RedirectResponse($createSession(), Response::HTTP_SEE_OTHER);
        } catch (ApiErrorException $e) {
            // Gateway hiccup: nothing customer-identifying in logs, generic flash for the user.
            $this->logger->warning('stripe.api.session_failed', ['error' => $e->getMessage()]);

            return $this->backToPortalWithError('portal.flash.gateway');
        }
    }

    private function backToPortalWithError(string $messageKey): RedirectResponse
    {
        $this->addFlash('error', $this->translator->trans($messageKey, [], 'api'));

        return $this->redirectToRoute('app_api_portal', status: Response::HTTP_SEE_OTHER);
    }
}
