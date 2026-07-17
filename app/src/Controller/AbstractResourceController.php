<?php
declare(strict_types=1);

namespace App\Controller;

use App\Dto\ClientData;
use App\Service\Client\ClientManager;
use App\Service\Client\PageContextResolver;
use App\Service\Client\VersionManager;
use Symfony\Bundle\FrameworkBundle\Controller\AbstractController;
use Symfony\Component\HttpFoundation\RequestStack;
use Symfony\Component\HttpFoundation\Response;

/**
 * Base des contrôleurs de ressource Data Dragon (champion / item / rune / summoner).
 *
 * Centralise les dépendances transverses et la gestion d'erreur « données absentes »
 * qui étaient dupliquées à l'identique dans chaque contrôleur (view-model client,
 * redirection vers le setup avec flash, formatage du message d'erreur).
 */
abstract class AbstractResourceController extends AbstractController
{
    public function __construct(
        protected readonly VersionManager $versionManager,
        protected readonly ClientManager $clientManager,
        protected readonly PageContextResolver $pageContext,
        protected readonly RequestStack $requestStack,
    ) {}

    /** View-model transverse (versions, langues, locale courante, préférences) injecté dans chaque vue. */
    protected function clientData(): ClientData
    {
        return ClientData::fromServices($this->versionManager, $this->clientManager);
    }

    /**
     * Redirige vers la configuration en signalant l'absence de données (version/langue
     * invalide ou panne upstream) via un flash d'erreur.
     *
     * @param array{version?:string, lang?:string} $ctx
     */
    protected function redirectToSetupWithError(array $ctx, \Throwable $e): Response
    {
        $this->requestStack->getSession()->getFlashBag()->clear();
        $this->addFlash('error', $this->dataError($ctx, $e));

        return $this->redirectToRoute('app_setup');
    }

    /**
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
