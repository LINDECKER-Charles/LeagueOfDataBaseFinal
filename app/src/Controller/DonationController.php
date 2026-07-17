<?php
declare(strict_types=1);

namespace App\Controller;

use App\Entity\User;
use App\Service\Client\ClientManager;
use App\Service\Client\PageContextResolver;
use App\Service\Client\VersionManager;
use App\Service\Donation\CheckoutSessionParams;
use App\Service\Donation\DonationTiers;
use App\Service\Donation\StripeCheckout;
use Psr\Log\LoggerInterface;
use Stripe\Exception\ApiErrorException;
use Symfony\Component\HttpFoundation\RedirectResponse;
use Symfony\Component\HttpFoundation\Request;
use Symfony\Component\HttpFoundation\RequestStack;
use Symfony\Component\HttpFoundation\Response;
use Symfony\Component\Routing\Attribute\Route;
use Symfony\Component\Routing\Generator\UrlGeneratorInterface;
use Symfony\Contracts\Translation\TranslatorInterface;

/**
 * Donation flow backed by Stripe Checkout — works without an account; when the
 * donor IS signed in, the session carries their user id so the webhook can
 * record the donation and grant the supporter badge. Extends the resource base
 * so pages provide the `client` view-model base.html.twig relies on.
 */
final class DonationController extends AbstractResourceController
{
    private const EUROS_PER_UNIT = 100;

    public function __construct(
        VersionManager $versionManager,
        ClientManager $clientManager,
        PageContextResolver $pageContext,
        RequestStack $requestStack,
        private readonly StripeCheckout $stripeCheckout,
        private readonly TranslatorInterface $translator,
        private readonly LoggerInterface $logger,
    ) {
        parent::__construct($versionManager, $clientManager, $pageContext, $requestStack);
    }

    #[Route('/donate', name: 'app_donate', methods: ['GET'])]
    public function index(): Response
    {
        return $this->render('donate/index.html.twig', [
            'client' => $this->clientData(),
            'configured' => $this->stripeCheckout->isConfigured(),
            'presets' => DonationTiers::PRESETS_CENTS,
            'minEuros' => intdiv(DonationTiers::MIN_CENTS, self::EUROS_PER_UNIT),
            'maxEuros' => intdiv(DonationTiers::MAX_CENTS, self::EUROS_PER_UNIT),
        ]);
    }

    #[Route('/donate/checkout', name: 'app_donate_checkout', methods: ['POST'])]
    public function checkout(Request $request): Response
    {
        if (!$this->isCsrfTokenValid('submit', (string) $request->request->get('_token'))) {
            return $this->rejectToDonate('donate.error.csrf');
        }
        if (!$this->stripeCheckout->isConfigured()) {
            return $this->rejectToDonate('donate.error.unavailable');
        }

        $amountCents = $this->resolveAmountCents($request);
        if ($amountCents === null) {
            return $this->rejectToDonate('donate.error.invalid_amount');
        }

        try {
            return new RedirectResponse(
                $this->stripeCheckout->createSession($this->buildParams($amountCents)),
                Response::HTTP_SEE_OTHER,
            );
        } catch (ApiErrorException $e) {
            // Gateway hiccup: no donor-identifying data in logs, generic flash for the user.
            $this->logger->warning('stripe.checkout.session_failed', ['error' => $e->getMessage()]);

            return $this->rejectToDonate('donate.error.gateway');
        }
    }

    #[Route('/donate/success', name: 'app_donate_success', methods: ['GET'])]
    public function success(): Response
    {
        // Deliberately no session retrieve / amount display: the thank-you page
        // stays generic so no Stripe call happens on the return path.
        return $this->render('donate/success.html.twig', ['client' => $this->clientData()]);
    }

    #[Route('/donate/cancel', name: 'app_donate_cancel', methods: ['GET'])]
    public function cancel(): Response
    {
        return $this->render('donate/cancel.html.twig', ['client' => $this->clientData()]);
    }

    /** Custom amount wins over the preset radio whenever the free field is filled. */
    private function resolveAmountCents(Request $request): ?int
    {
        $custom = trim((string) $request->request->get('amount', ''));
        if ($custom !== '') {
            return DonationTiers::normalizeEuroInput($custom);
        }

        $preset = $request->request->getInt('preset');

        return DonationTiers::isPreset($preset) ? $preset : null;
    }

    /** @return array<string, mixed> */
    private function buildParams(int $amountCents): array
    {
        $params = CheckoutSessionParams::build(
            $amountCents,
            $this->translator->trans('donate.product_name'),
            $this->generateUrl('app_donate_success', [], UrlGeneratorInterface::ABSOLUTE_URL),
            $this->generateUrl('app_donate_cancel', [], UrlGeneratorInterface::ABSOLUTE_URL),
        );

        $user = $this->getUser();

        return $user instanceof User && $user->getId() !== null
            ? CheckoutSessionParams::forDonor($params, $user->getId())
            : $params;
    }

    private function rejectToDonate(string $messageKey): RedirectResponse
    {
        $this->addFlash('error', $this->translator->trans($messageKey));

        return $this->redirectToRoute('app_donate');
    }
}
