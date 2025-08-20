<?php
declare(strict_types=1);

namespace App\Controller;

use App\Dto\ClientData;
use App\Service\ItemManager;
use App\Service\ClientManager;
use App\Service\VersionManager;
use Symfony\Component\HttpFoundation\Response;
use Symfony\Component\Routing\Annotation\Route;
use Symfony\Component\HttpFoundation\JsonResponse;
use Symfony\Component\HttpFoundation\RequestStack;
use Symfony\Bundle\FrameworkBundle\Controller\AbstractController;

final class ItemController extends AbstractController{
    public function __construct(
        private readonly ClientManager $client,
        private readonly VersionManager $versionManager, 
        private readonly ClientManager $clientManager,
        private readonly RequestStack $requestStack,
        private readonly ItemManager $itemManager,
    ) {}


    /**
     * Redirige vers la liste des objets (items) avec pagination.
     *
     * Ajoute les paramètres de version/langue depuis la session et applique une limite
     * au nombre d’éléments par page (max 20).
     *
     * @param int $numpage      Numéro de page.
     * @param int $itemperpage  Nombre d’items par page (max 20).
     *
     * @return Response Redirection vers la route app_items.
     */
    #[Route('/objects_redirect/{numpage}/{itemperpage}', 
        name: 'app_items_redirect', 
        methods: ['GET'], 
        defaults: ['numpage' => 1,'itemperpage' => 9]
    )]
    public function objects_redirect(int $numpage, int $itemperpage): Response{
        // On recupere les informations en session
        $session = $this->client->getSession();
        
        // On les ajoutes en parametre dans l URL
        return $this->redirectToRoute('app_items', [
            'version' => $session['version'],
            'lang' => $session['lang'],
            'numpage' => $numpage,
            'itemperpage' => $itemperpage > 20 ? 20 : $itemperpage,
        ]);
    }

    /**
     * Affiche la liste paginée des objets (items).
     *
     * Récupère les paramètres (version, langue, pagination) depuis la session.
     * Si les paramètres sont absents, redirige vers la route de redirection.
     * Les données paginées incluent les items, leurs images et des métadonnées.
     *
     * @return Response Vue Twig affichant la liste paginée des items.
     */
    #[Route('/objects', name: 'app_items', methods: ['GET'])]
    public function objects(): Response{
        // 1) On récupère les paramètres
        $session = $this->client->getParams();

        // 1.1) Si nos paramètres ne sont pas défini alors on les définis via la redirection
        if(!$session['param']){
            /* dd($session); */
            return $this->redirectToRoute('app_items_redirect');
        }

        try {
            $data = $this->itemManager->paginate($session['version'], $session['lang'], $session['itemPerPage'] > 20 ? 20 : $session['itemPerPage'], $session['numPage']);
            /* dd($data, $data['images']); */
        } catch (\Throwable $e) {
            $this->requestStack->getSession()->getFlashBag()->clear();
            $this->addFlash('error', sprintf(
                "Donnés absente sur la version %s et la langue %s Message --> %s",
                $session['version'] ?? 'n/a',
                $session['lang'] ?? 'n/a',
                $e->getMessage()
            ));
            return $this->redirectToRoute('app_setup');
        }
        return $this->render('item/liste.html.twig', [
            'items' => $data['items'],
            'images'    => $data['images'],
            'meta' => $data['meta'],
            'client' => ClientData::fromServices($this->versionManager, $this->clientManager),
        ]);
    }

    
    
    #[Route('/object_redirect/{name}', name: 'app_item_redirect', methods: ['GET'])]
    public function summoner_redirect(string $name): Response
    {
        // On récupère les informations en session
        $session = $this->clientManager->getSession();
        /* dd(); */
        // On les ajoute en paramètre dans l'URL de la page détail
        return $this->redirectToRoute('app_item', [
            'name'    => $name,
            'version' => $session['version'],
            'lang'    => $session['lang'],
        ]);
    }

    
    #[Route('/object/{name}', name: 'app_item', methods: ['GET'])]
    public function summoner(string $name): Response{

        $session = $this->clientManager->getParams(['version', 'lang']);

        // Si pas de paramètres valides → redirection
        if (!$session['param']) {
            return $this->redirectToRoute('app_summoner_redirect', ['name' => $name]);
        }

        try {
            $image = $this->itemManager->getImage($name . '.png', $session['version'], [], false, $session['lang']);
            $item = $this->itemManager->getByName($name, $session['version'], $session['lang']);
        } catch (\Throwable $e) {
            $this->requestStack->getSession()->getFlashBag()->clear();
            $this->addFlash('error', sprintf(
                "Donnés absente sur la version %s et la langue %s Message --> %s",
                $session['version'] ?? 'n/a',
                $session['lang'] ?? 'n/a',
                $e->getMessage()
            ));
            return $this->redirectToRoute('app_setup');
        }

        return $this->render('item/detail.html.twig', [
            'item' => $item,
            'image'    => $image,
            'client' => ClientData::fromServices($this->versionManager, $this->clientManager),
        ]);
    }

    #[Route('/api/objects/search/{name}', name: 'api_objects_search', methods: ['GET'])]
    public function searchSummonersApi(string $name): JsonResponse
    {
        $session = $this->clientManager->getSession();
        try {
            $items = $this->itemManager->searchByName($name, $session['version'], $session['lang'], 20);
            $images = $this->itemManager->getImages($session['version'], $session['lang'], false, $items);
        } catch (\Throwable $e) {
            return $this->json( sprintf(
                "Donnés absente sur la version %s et la langue %s Message --> %s",
                $session['version'] ?? 'n/a',
                $session['lang'] ?? 'n/a',
                $e->getMessage()
            ));
        }
    
        // Filtrer uniquement id, name et image
        $final = array_map(function ($item) use ($images) {
            $id = $item['id'] ?? null;
            $name = $item['name'] ?? null;
            return [
                'id'    => $id,
                'name'  => $item['name'] ?? '',
                'image' => $name && isset($images[$name]) ? $images[$name] : null,
            ];
        }, $items);
        /* dd($final, $items, $images); */
        return $this->json($final);
    }
}
