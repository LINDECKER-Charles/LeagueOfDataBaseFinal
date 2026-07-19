<?php
declare(strict_types=1);

namespace App\Controller;

use App\Service\API\ItemManager;
use App\Service\Client\ClientManager;
use App\Service\Client\PageContextResolver;
use App\Service\Client\VersionManager;
use Symfony\Component\HttpFoundation\Response;
use Symfony\Component\Routing\Annotation\Route;
use Symfony\Component\HttpFoundation\JsonResponse;
use Symfony\Component\HttpFoundation\RequestStack;

final class ItemController extends AbstractResourceController
{
    public function __construct(
        VersionManager $versionManager,
        ClientManager $clientManager,
        PageContextResolver $pageContext,
        RequestStack $requestStack,
        private readonly ItemManager $itemManager,
    ) {
        parent::__construct($versionManager, $clientManager, $pageContext, $requestStack);
    }

    /**
     * Liste paginée des objets. Version/langue viennent de la query (URL
     * partageable + cacheable), sinon de la sélection en session — sans redirect.
     */
    #[Route('/objects', name: 'app_items', methods: ['GET'])]
    #[Route('/{version}/objects', name: 'app_items_versioned', requirements: ['version' => AbstractResourceController::VERSION_ROUTE_REQUIREMENT], methods: ['GET'])]
    public function objects(): Response
    {
        // Full list in one render — the ResourceFilter island owns search, tag
        // facets and pagination client-side; only version/lang matter server-side.
        $sel = $this->pageContext->selection();

        try {
            $data = $this->itemManager->paginate($sel['version'], $sel['lang'], 0, 1);
        } catch (\Throwable $e) {
            return $this->redirectToHomeWithError($sel, $e);
        }

        $data['meta']['version'] = $sel['version'];
        $data['meta']['lang']    = $sel['lang'];

        // Résout les ids d'évolution (item.into) en objets liables (nom + icône +
        // prix) pour l'accordéon « Évolutions » — une passe pour toute la page.
        $related = $this->itemManager->relatedIndex($data['items'], $sel['version'], $sel['lang']);

        return $this->render('item/liste.html.twig', [
            'items'   => $data['items'],
            'images'  => $data['images'],
            'related' => $related,
            'meta'    => $data['meta'],
            'client'  => $this->clientData(),
        ]);
    }

    /**
     * Détail d'un objet. Version/langue résolues depuis la query, sinon la session.
     */
    #[Route('/object/{name}', name: 'app_item', methods: ['GET'])]
    #[Route('/{version}/object/{name}', name: 'app_item_versioned', requirements: ['version' => AbstractResourceController::VERSION_ROUTE_REQUIREMENT], methods: ['GET'])]
    public function object(string $name): Response
    {
        $sel = $this->pageContext->selection();

        try {
            // Lookup first: an unknown slug must 404 from the dataset alone,
            // without ever asking the CDN for an image that cannot exist.
            $item  = $this->itemManager->getByName($name, $sel['version'], $sel['lang']);
            $image = $this->itemManager->getImage($sel['version'], $name . '.png');
            // Les IDs de item.into / item.from ne parlent pas au joueur : on les
            // résout en objets réels (nom + image + prix) liables vers leur page
            // détail. `components` (from) + cet objet + `related` (into) forment
            // l'arbre de recette affiché par le template.
            $related = $this->itemManager->resolveRelated($item['into'] ?? [], $sel['version'], $sel['lang']);
            // Arbre de recette descendant complet (composants récursifs) plutôt
            // qu'un seul niveau — le vrai « arbre de craft » de l'objet.
            $recipe  = $this->itemManager->recipeTree($name, $sel['version'], $sel['lang']);
        } catch (\Throwable $e) {
            return $this->detailFailure($sel, $e);
        }

        return $this->render('item/detail.html.twig', [
            'item'       => $item,
            'image'      => $image,
            'related'    => $related,
            'recipeTree' => $recipe,
            'version'    => $sel['version'],
            'lang'       => $sel['lang'],
            'nav'        => $this->neighbors($this->itemManager, $sel['version'], $sel['lang'], $name),
            'client'     => $this->clientData(),
        ]);
    }

    /**
     * API : recherche d'objets par nom → JSON simplifié {id, name, image}.
     */
    #[Route('/api/objects/search/{name}', name: 'api_objects_search', methods: ['GET'])]
    public function searchItemsApi(string $name): JsonResponse
    {
        $session = $this->clientManager->getSession();
        try {
            $items = $this->itemManager->searchByName($name, $session['version'], $session['lang'], 20);
        } catch (\Throwable $e) {
            return $this->json($this->dataError($session, $e));
        }

        if (empty($items)) {
            return $this->json([]);
        }

        $images = $this->itemManager->getImages($session['version'], $session['lang'], false, $items);

        $final = array_map(static function ($item) use ($images) {
            $name = $item['name'] ?? null;
            return [
                'id'    => $item['id'] ?? null,
                'name'  => $item['name'] ?? '',
                'image' => $name && isset($images[$name]) ? $images[$name] : null,
            ];
        }, $items);

        return $this->json($final);
    }
}
