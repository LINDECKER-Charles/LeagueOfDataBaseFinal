<?php
declare(strict_types=1);

namespace App\Service\PublicApi;

use App\Entity\ApiPlan;
use App\Entity\User;
use App\Service\Donation\StripeCheckout;
use Symfony\Component\Routing\Generator\UrlGeneratorInterface;
use Symfony\Contracts\Translation\TranslatorInterface;

/**
 * Checkout entry point for the API billing (credit packs + plan
 * subscriptions). Deliberately reuses the donation StripeCheckout gateway so
 * the Stripe secret key keeps a single holder; this class only owns the API
 * payloads and the portal return URLs.
 */
final readonly class ApiCheckout
{
    public const STATUS_PACK_SUCCESS = 'pack_success';
    public const STATUS_PLAN_SUCCESS = 'plan_success';
    public const STATUS_CANCELLED = 'cancelled';

    private const TRANSLATION_DOMAIN = 'api';

    public function __construct(
        private StripeCheckout $gateway,
        private TranslatorInterface $translator,
        private UrlGeneratorInterface $urlGenerator,
    ) {}

    public function isConfigured(): bool
    {
        return $this->gateway->isConfigured();
    }

    /**
     * @return string Stripe hosted-page URL
     *
     * @throws \Stripe\Exception\ApiErrorException
     */
    public function packSession(User $user, ApiCreditPack $pack): string
    {
        $label = $this->translator->trans(
            'product.pack',
            ['%requests%' => $pack->requests()],
            self::TRANSLATION_DOMAIN,
        );

        return $this->gateway->createSession(ApiCheckoutParams::pack(
            (int) $user->getId(),
            $pack,
            $label,
            $this->returnUrls(self::STATUS_PACK_SUCCESS),
        ));
    }

    /**
     * @return string Stripe hosted-page URL
     *
     * @throws \Stripe\Exception\ApiErrorException
     */
    public function planSession(User $user, ApiPlan $plan): string
    {
        $label = $this->translator->trans(
            'product.plan',
            ['%plan%' => $this->translator->trans('plan.' . $plan->value, [], self::TRANSLATION_DOMAIN)],
            self::TRANSLATION_DOMAIN,
        );

        return $this->gateway->createSession(ApiCheckoutParams::plan(
            (int) $user->getId(),
            $plan,
            $label,
            $this->returnUrls(self::STATUS_PLAN_SUCCESS),
        ));
    }

    private function returnUrls(string $successStatus): CheckoutReturnUrls
    {
        return new CheckoutReturnUrls(
            $this->portalUrl($successStatus),
            $this->portalUrl(self::STATUS_CANCELLED),
        );
    }

    private function portalUrl(string $status): string
    {
        return $this->urlGenerator->generate(
            'app_api_portal',
            ['status' => $status],
            UrlGeneratorInterface::ABSOLUTE_URL,
        );
    }
}
