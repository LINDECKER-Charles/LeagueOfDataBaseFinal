<?php
declare(strict_types=1);

namespace App\Controller;

use App\Dto\ClientData;
use App\Service\API\AbstractManager;
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
 * redirection vers la home avec flash, formatage du message d'erreur).
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
     * Redirige vers la home en signalant l'absence de données (version/langue
     * invalide ou panne upstream) via un flash d'erreur — le switcher du header
     * y permet de corriger la sélection.
     *
     * @param array{version?:string, lang?:string} $ctx
     */
    protected function redirectToHomeWithError(array $ctx, \Throwable $e): Response
    {
        $this->requestStack->getSession()->getFlashBag()->clear();
        $this->addFlash('error', $this->dataError($ctx, $e));

        return $this->redirectToRoute('app_home');
    }

    /**
     * Voisins immédiats d'une entrée dans l'ordre de sa collection — alimente la
     * navigation précédent/suivant des pages détail. Meilleur-effort : toute
     * erreur de données rend une navigation vide, jamais une page cassée.
     *
     * @return array{prev: ?array{id: string, name: string}, next: ?array{id: string, name: string}}
     */
    protected function neighbors(AbstractManager $manager, string $version, string $lang, string $currentId): array
    {
        try {
            $index = $manager->listIndex($version, $lang);
        } catch (\Throwable) {
            return ['prev' => null, 'next' => null];
        }

        // PHP recasts numeric-string array keys to int — normalise back so the
        // strict search matches route ids like "3153".
        $ids = array_map(strval(...), array_keys($index));
        $pos = array_search($currentId, $ids, true);
        if ($pos === false) {
            return ['prev' => null, 'next' => null];
        }

        $at = static fn (int $i): ?array => isset($ids[$i])
            ? ['id' => (string) $ids[$i], 'name' => $index[$ids[$i]]]
            : null;

        return ['prev' => $at($pos - 1), 'next' => $at($pos + 1)];
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
