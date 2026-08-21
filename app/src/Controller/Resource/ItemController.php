<?php
declare(strict_types=1);

namespace App\Controller\Resource;

use App\Service\API\DatasetRef;
use App\Service\API\Edition\ItemEditionRule;
use App\Service\API\ItemManager;
use App\Service\Client\ClientManager;
use App\Service\Client\PageContextResolver;
use App\Service\Client\VersionManager;
use Symfony\Component\HttpFoundation\JsonResponse;
use Symfony\Component\HttpFoundation\RequestStack;
use Symfony\Component\HttpFoundation\Response;
use Symfony\Component\Routing\Attribute\Route;

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
     * Paginated item list. Version/lang come from the query (shareable +
     * cacheable URL), otherwise from the session selection — without any redirect.
     */
    #[Route('/objects', name: 'app_items', methods: ['GET'])]
    #[Route(
        '/{version}/objects',
        name: 'app_items_versioned',
        requirements: ['version' => AbstractResourceController::VERSION_ROUTE_REQUIREMENT],
        methods: ['GET']
    )]
    public function objects(): Response
    {
        return $this->listPage(
            $this->itemManager,
            'item/liste.html.twig',
            // Resolves upgrade ids (item.into) into linkable objects (name + icon +
            // price) for the "Évolutions" accordion — one pass for the whole page.
            fn (array $data, string $version, string $lang): array => [
                'related' => $this->itemManager->relatedIndex($data['items'], $version, $lang),
            ],
        );
    }

    /**
     * Item detail. Version/lang resolved from the query, otherwise the session.
     */
    #[Route('/object/{name}', name: 'app_item', methods: ['GET'])]
    #[Route(
        '/{version}/object/{name}',
        name: 'app_item_versioned',
        requirements: ['version' => AbstractResourceController::VERSION_ROUTE_REQUIREMENT],
        methods: ['GET']
    )]
    public function object(string $name): Response
    {
        $selection = $this->pageContext->selection();
        ['version' => $version, 'lang' => $lang] = $selection;

        try {
            $view = $this->detailView($name, $version, $lang);
        } catch (\Throwable $e) {
            return $this->detailFailure($selection, $e, 'app_items_versioned');
        }

        return $this->render('item/detail.html.twig', $view + [
            'version'    => $version,
            'lang'       => $lang,
            'nav'        => $this->neighbors($this->itemManager, $selection, $name),
            'client'     => $this->clientData(),
        ]);
    }

    /**
     * API: item search by name → simplified JSON {id, name, image}.
     */
    #[Route('/api/objects/search/{name}', name: 'api_objects_search', methods: ['GET'])]
    public function searchItemsApi(string $name): JsonResponse
    {
        return $this->searchResponse(
            $this->itemManager,
            $name,
            // ItemManager::getImages keys its map by item ID.
            static fn (array $rows, array $images): array => array_map(
                static fn (array $row): ?string => $images[(string) ($row['id'] ?? '')] ?? null,
                $rows,
            ),
        );
    }

    /**
     * Everything the detail page needs from the data layer, in one guarded pass —
     * any failure here decides the page's HTTP outcome ({@see detailFailure()}).
     *
     * `item.into` / `item.from` ids mean nothing to the player: they are resolved
     * into real objects (name + image + price) linkable to their own detail page.
     * `components` (from) + this object + `related` (into) form the recipe tree
     * rendered by the template.
     *
     * @return array<string, mixed>
     */
    private function detailView(string $name, string $version, string $lang): array
    {
        // Lookup first: an unknown slug must 404 from the dataset alone, without
        // ever asking the CDN for an image that cannot exist.
        $item = $this->itemManager->getByName($name, $version, $lang);
        $into = $item['into'] ?? [];

        return [
            'item'          => $item,
            'image'         => $this->itemManager->getImage($version, $name . '.png'),
            'related'       => $this->itemManager->resolveRelated($into, $version, $lang),
            // Full downward recipe tree (recursive components) rather than a single
            // level — the item's real crafting tree.
            'recipeTree'    => $this->itemManager->recipeTree($name, $version, $lang),
            // Which game this item belongs to, and its twin in the other one
            // (1004 ↔ 771004) when the patch carries it. The availability
            // plaque never trusts the raw maps flags ({@see ItemEditionRule}).
            'edition'       => $this->itemManager->editionOf($name, $item)->value,
            'availableMaps' => ItemEditionRule::claimableMapIds(
                $name,
                (array) ($item['maps'] ?? []),
            ),
            'counterpart'   => $this->itemManager->counterpart(
                $name,
                new DatasetRef($version, $lang),
            ),
        ];
    }
}
