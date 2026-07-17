<?php
declare(strict_types=1);

namespace App\Controller;

use App\Entity\ApiPlan;
use App\Service\PublicApi\ApiCreditPack;
use App\Service\PublicApi\ApiKeyIssuer;
use Symfony\Component\HttpFoundation\Response;
use Symfony\Component\Routing\Attribute\Route;

/**
 * Public, indexable presentation of the paid API: what it serves, how keys
 * authenticate, the pricing grid and copy-paste curl examples. The CTA sends
 * to the portal (/profile/api), behind login.
 */
final class DevelopersController extends AbstractResourceController
{
    /**
     * v1: the go-api service answers on its own port; production exposure will
     * go through the TLS edge under the site's domain (documented on the page).
     */
    private const API_BASE_URL = 'http://localhost:8090';

    #[Route('/developers', name: 'app_developers', methods: ['GET'])]
    public function index(): Response
    {
        return $this->render('developers/index.html.twig', [
            'client' => $this->clientData(),
            'apiBaseUrl' => self::API_BASE_URL,
            'keyPrefix' => ApiKeyIssuer::RAW_PREFIX,
            'freePlan' => ApiPlan::Free,
            'packs' => ApiCreditPack::cases(),
            'subscriptions' => ApiPlan::subscriptions(),
            'creditRate' => ApiPlan::RATE_CREDITS,
        ]);
    }
}
