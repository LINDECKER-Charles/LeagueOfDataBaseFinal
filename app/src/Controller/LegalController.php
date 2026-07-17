<?php
declare(strict_types=1);

namespace App\Controller;

use App\Dto\LegalInfo;
use App\Service\Client\ClientManager;
use App\Service\Client\PageContextResolver;
use App\Service\Client\VersionManager;
use Symfony\Component\HttpFoundation\Request;
use Symfony\Component\HttpFoundation\RequestStack;
use Symfony\Component\HttpFoundation\Response;
use Symfony\Component\Routing\Attribute\Route;

/**
 * Static legal pages (notice / privacy / terms / cookies). Legalese cannot be
 * sliced into translation keys, so the body is maintained as two full template
 * variants: any "fr*" UI locale gets the French text, every other locale falls
 * back to the English reference version. Only chrome strings go through |trans.
 */
final class LegalController extends AbstractResourceController
{
    private const LOCALE_DIR_FR = 'fr';
    private const LOCALE_DIR_EN = 'en';

    public function __construct(
        VersionManager $versionManager,
        ClientManager $clientManager,
        PageContextResolver $pageContext,
        RequestStack $requestStack,
        private readonly LegalInfo $legalInfo,
    ) {
        parent::__construct($versionManager, $clientManager, $pageContext, $requestStack);
    }

    #[Route('/legal/notice', name: 'app_legal_notice', methods: ['GET'])]
    public function notice(Request $request): Response
    {
        return $this->renderLocalizedPage('notice', $request);
    }

    #[Route('/legal/privacy', name: 'app_legal_privacy', methods: ['GET'])]
    public function privacy(Request $request): Response
    {
        return $this->renderLocalizedPage('privacy', $request);
    }

    #[Route('/legal/terms', name: 'app_legal_terms', methods: ['GET'])]
    public function terms(Request $request): Response
    {
        return $this->renderLocalizedPage('terms', $request);
    }

    #[Route('/legal/cookies', name: 'app_legal_cookies', methods: ['GET'])]
    public function cookies(Request $request): Response
    {
        return $this->renderLocalizedPage('cookies', $request);
    }

    private function renderLocalizedPage(string $page, Request $request): Response
    {
        $localeDir = str_starts_with($request->getLocale(), self::LOCALE_DIR_FR)
            ? self::LOCALE_DIR_FR
            : self::LOCALE_DIR_EN;

        return $this->render(sprintf('legal/%s/%s.html.twig', $localeDir, $page), [
            'client'      => $this->clientData(),
            'legal'       => $this->legalInfo,
            'lastUpdated' => $this->legalInfo->effectiveDate,
        ]);
    }
}
