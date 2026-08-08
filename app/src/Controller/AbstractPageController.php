<?php
declare(strict_types=1);

namespace App\Controller;

use App\Dto\ClientData;
use App\Service\Client\ClientManager;
use App\Service\Client\PageContextResolver;
use App\Service\Client\VersionManager;
use Symfony\Bundle\FrameworkBundle\Controller\AbstractController;
use Symfony\Component\HttpFoundation\RequestStack;

/**
 * Base of every controller rendering a full page of the site: it owns the
 * cross-cutting view-model `base.html.twig` relies on (`client`) and the shared
 * "missing data" message, nothing more.
 *
 * Deliberately separated from
 * {@see \App\Controller\Resource\AbstractResourceController}: most pages (auth,
 * legal, profile, builds…) only need the transverse view-model and have no
 * business with the Data Dragon collection navigation — inheriting
 * `neighbors()` or `detailFailure()` there was meaningless.
 */
abstract class AbstractPageController extends AbstractController
{
    public function __construct(
        protected readonly VersionManager $versionManager,
        protected readonly ClientManager $clientManager,
        protected readonly PageContextResolver $pageContext,
        protected readonly RequestStack $requestStack,
    ) {}

    /**
     * Cross-cutting view-model (versions, languages, current locale, preferences)
     * injected into every view.
     */
    protected function clientData(): ClientData
    {
        return ClientData::fromServices($this->versionManager, $this->clientManager);
    }

    /**
     * Player-facing description of a data-layer failure, used as a flash message.
     * Never returned as an API payload — machine callers get a status code and a
     * neutral body instead
     * ({@see \App\Controller\Resource\AbstractResourceController::searchResponse}).
     *
     * @param array{version?:string, lang?:string} $ctx
     */
    protected function dataError(array $ctx, \Throwable $e): string
    {
        return sprintf(
            'Donnés absente sur la version %s et la langue %s Message --> %s',
            $ctx['version'] ?? 'n/a',
            $ctx['lang'] ?? 'n/a',
            $e->getMessage()
        );
    }
}
