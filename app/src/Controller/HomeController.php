<?php
declare(strict_types=1);

namespace App\Controller;

use App\Service\API\AbstractManager;
use App\Service\API\ChampionManager;
use App\Service\API\DatasetRef;
use App\Service\API\ItemManager;
use App\Service\API\RuneManager;
use App\Service\API\SummonerManager;
use App\Service\Client\ClientManager;
use App\Service\Client\PageContextResolver;
use App\Service\Client\VersionManager;
use App\Service\Tools\UrlGenerator;
use Symfony\Component\HttpFoundation\Cookie;
use Symfony\Component\HttpFoundation\RedirectResponse;
use Symfony\Component\HttpFoundation\Request;
use Symfony\Component\HttpFoundation\RequestStack;
use Symfony\Component\HttpFoundation\Response;
use Symfony\Component\Routing\Attribute\Route;

final class HomeController extends AbstractPageController
{
    /**
     * Persistence window (days) for the signed patch+language preference cookie.
     * A chosen patch/language is a functional preference (not tracking), so the
     * switcher always persists it — the "remember" toggle only widens the window
     * from the default to the extended one. Version already rides the URL;
     * language has no URL carrier and would otherwise fall back to the domain
     * default (en on .com) on the next session without this cookie.
     */
    private const PREF_COOKIE_DAYS = 30;
    private const PREF_COOKIE_DAYS_EXTENDED = 365;

    public function __construct(
        VersionManager $versionManager,
        ClientManager $clientManager,
        PageContextResolver $pageContext,
        RequestStack $requestStack,
        private readonly UrlGenerator $urlGenerator,
        private readonly ItemManager $itemManager,
        private readonly SummonerManager $summonerManager,
        private readonly ChampionManager $championManager,
        private readonly RuneManager $runeManager,
    ) {
        parent::__construct($versionManager, $clientManager, $pageContext, $requestStack);
    }

    #[Route('/working-progress', name: 'app_working')]
    public function working(): Response
    {
        return $this->render('home/working.html.twig', ['client' => $this->clientData()]);
    }

    /**
     * Version/language selection form handler.
     *
     * Security notes: the Referer is checked to stay on the same host (home page
     * as fallback), and the "remember" cookie is HMAC-signed for integrity only —
     * it carries no confidentiality guarantee.
     */
    #[Route('/setup-submit', name: 'app_setup_save', methods: ['POST'])]
    public function save(Request $request): RedirectResponse
    {
        $language = (string) $request->request->get('langue', '');
        $version  = (string) $request->request->get('version', '');
        $backUrl  = $this->urlGenerator->generateBackUrl();

        $report = $this->versionManager->validateSelection($version, $language);
        if (!$report['ok']) {
            return $this->rejectSelection($request, $report['errors'], $backUrl);
        }

        // Apply the selection to the return URL: the version goes into the path
        // segment when the URL is versioned (/{version}/…), as a query parameter
        // otherwise — without this a ?version= would be shadowed by the previous
        // segment (path wins over query).
        $response = $this->redirect(
            $this->urlGenerator->applySelection($backUrl, $version, $language)
        );
        $response->headers->setCookie($this->preferenceCookie($request, $language, $version));

        $this->clientManager->setLocaleInSession($language);
        $this->clientManager->setVersionInSession($version);

        $request->getSession()?->getFlashBag()->clear();
        $this->addFlash('success', 'Preferences saved');

        return $response;
    }

    #[Route('/', name: 'app_home')]
    public function home(): Response
    {
        $dataset = DatasetRef::fromSelection($this->pageContext->selection());

        return $this->render('home/home.html.twig', [
            'client'    => $this->clientData(),
            'champions' => $this->preview($this->championManager, $dataset),
            'items'     => $this->preview($this->itemManager, $dataset),
            'summoners' => $this->preview($this->summonerManager, $dataset),
            'runes'     => $this->preview($this->runeManager, $dataset),
        ]);
    }

    /**
     * Legacy home URL. Permanent redirect: installed PWAs still carry `/home` as
     * their start_url (cached manifest) and external links still reference it.
     */
    #[Route('/home', name: 'app_home_legacy')]
    public function homeLegacy(): RedirectResponse
    {
        return $this->redirectToRoute('app_home', status: Response::HTTP_MOVED_PERMANENTLY);
    }

    /**
     * Invalid selection: the visitor goes back where they came from with one flash
     * per rejected field, and nothing is persisted.
     *
     * @param array<string, string> $errors
     */
    private function rejectSelection(
        Request $request,
        array $errors,
        string $backUrl,
    ): RedirectResponse {
        $request->getSession()?->getFlashBag()->clear();
        foreach ($errors as $field => $msg) {
            $this->addFlash('error', sprintf('%s: %s', ucfirst($field), $msg));
        }

        return $this->redirect($backUrl);
    }

    /**
     * The selection is a functional preference, so it is ALWAYS persisted in the
     * signed cookie (language has no URL carrier — without this cookie it would
     * fall back to the domain default on the next session, where the version
     * survives through the URL). The "remember" checkbox only widens the lifetime.
     */
    private function preferenceCookie(Request $request, string $language, string $version): Cookie
    {
        $days = $request->request->getBoolean('remember')
            ? self::PREF_COOKIE_DAYS_EXTENDED
            : self::PREF_COOKIE_DAYS;

        return $this->clientManager->makeRememberCookie($language, $version, $days);
    }

    /**
     * Renders one home preview in isolation: a failing resource (transient
     * upstream outage) yields an empty section instead of taking the whole page
     * down. A legitimate absence of data is already neutralised upstream (empty
     * set from the managers); this guard covers the errors the data layer
     * deliberately propagates. The empty shape keeps the keys the template
     * expects (`<type>s`, `images`, `meta`) so it stays compatible with
     * strict_variables — derived from the manager so it can never drift.
     *
     * @return array<mixed>
     */
    private function preview(AbstractManager $manager, DatasetRef $dataset): array
    {
        try {
            // The loader pre-warms exactly this many images per resource, hence
            // the shared constant rather than a literal on each side.
            return $manager->paginate($dataset, PageContextResolver::HOME_PER_PAGE);
        } catch (\Throwable) {
            return [$manager->type() . 's' => [], 'images' => [], 'meta' => []];
        }
    }
}
