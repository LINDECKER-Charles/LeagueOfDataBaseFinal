<?php
declare(strict_types=1);

namespace App\Controller;

use App\Service\Client\ClientManager;
use App\Service\Client\PageContextResolver;
use App\Service\Client\VersionManager;
use App\Service\Seo\SiteInventory;
use Symfony\Component\HttpFoundation\RequestStack;
use Symfony\Component\HttpFoundation\Response;
use Symfony\Component\Routing\Attribute\Route;
use Symfony\Contracts\Translation\TranslatorInterface;

/**
 * Editorial pages describing the project itself: what it is (/about), where the
 * data comes from (/about/data) and the questions people actually ask (/faq).
 *
 * These exist for discoverability: nothing else on the site states in prose what
 * the project is, who it is for, or how it sources its data — which is exactly
 * what a search or answer engine needs in order to describe it.
 */
final class AboutController extends AbstractResourceController
{
    private const CATALOG = 'about';

    /**
     * FAQ entry ids, in display order. This list is the contract; the copy lives
     * in the `about` catalog under faq.<id>.question / faq.<id>.answer. The page
     * and its FAQPage structured data are both driven from here, so the rendered
     * answers and the ones exposed to crawlers can never drift apart.
     */
    private const FAQ_ENTRIES = [
        'what-is-it',
        'data-source',
        'freshness',
        'languages',
        'old-patches',
        'free',
        'account',
        'builds',
        'api',
        'offline',
        'riot-affiliation',
    ];

    public function __construct(
        VersionManager $versionManager,
        ClientManager $clientManager,
        PageContextResolver $pageContext,
        RequestStack $requestStack,
        private readonly TranslatorInterface $translator,
        private readonly SiteInventory $inventory,
    ) {
        parent::__construct($versionManager, $clientManager, $pageContext, $requestStack);
    }

    #[Route('/about', name: 'app_about', methods: ['GET'])]
    public function index(): Response
    {
        return $this->render('about/index.html.twig', [
            'client'    => $this->clientData(),
            'inventory' => $this->inventory->snapshot(),
        ]);
    }

    #[Route('/about/data', name: 'app_about_data', methods: ['GET'])]
    public function data(): Response
    {
        return $this->render('about/data.html.twig', [
            'client'    => $this->clientData(),
            'inventory' => $this->inventory->snapshot(),
        ]);
    }

    #[Route('/faq', name: 'app_faq', methods: ['GET'])]
    public function faq(): Response
    {
        return $this->render('about/faq.html.twig', [
            'client'  => $this->clientData(),
            'entries' => $this->faqEntries(),
        ]);
    }

    /**
     * Resolves every FAQ entry once, for both the visible list and the FAQPage node.
     *
     * @return list<array{id:string, question:string, answer:string}>
     */
    private function faqEntries(): array
    {
        return array_map(fn (string $id): array => [
            'id'       => $id,
            'question' => $this->translator->trans(sprintf('faq.%s.question', $id), [], self::CATALOG),
            'answer'   => $this->translator->trans(sprintf('faq.%s.answer', $id), [], self::CATALOG),
        ], self::FAQ_ENTRIES);
    }
}
