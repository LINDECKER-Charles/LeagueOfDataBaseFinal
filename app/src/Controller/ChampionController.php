<?php
declare(strict_types=1);

namespace App\Controller;

use App\Dto\ClientData;
use App\Service\API\ChampionManager;
use App\Service\Client\ClientManager;
use App\Service\Client\VersionManager;
use Symfony\Component\HttpFoundation\JsonResponse;
use Symfony\Component\HttpFoundation\RequestStack;
use Symfony\Bundle\FrameworkBundle\Controller\AbstractController;
use Symfony\Component\HttpFoundation\Response;
use Symfony\Component\Routing\Annotation\Route;

final class ChampionController extends AbstractController{
    public function __construct(
        private readonly VersionManager $versionManager, 
        private readonly ClientManager $clientManager,
        private readonly RequestStack $requestStack,
        private readonly ChampionManager $championManager,
    ) {}



    #[Route('/champions_redirect/{numpage}/{itemperpage}', 
        name: 'app_champions_redirect', 
        methods: ['GET'], 
        defaults: ['numpage' => 1,'itemperpage' => 20]
    )]
    public function champions_redirect(int $numpage, int $itemperpage): Response{
        // On recupere les informations en session

        $session = $this->clientManager->getSession();
        
        // On les ajoutes en parametre dans l URL
        return $this->redirectToRoute('app_champions', [
            'version' => $session['version'],
            'lang' => $session['lang'],
            'numpage' => $numpage,
            'itemperpage' => $itemperpage > 20 ? 20 : $itemperpage,
        ]);
    }

    /**
     * Affiche la liste paginée des champions disponibles.
     *
     * Récupère la version et la langue depuis la session client,
     * puis charge la liste complète des champions via le ChampionManager.
     * Fournit à la vue les images, les données et les métadonnées de pagination.
     *
     * @param Request $request Requête HTTP contenant les paramètres de page (optionnels).
     *
     * @return Response Page affichant la liste des champions avec pagination.
     *
     * @throws \Throwable Si une erreur survient lors du chargement des données ou de la communication avec l’API.
     */
    #[Route('/champions', name: 'app_champions', methods: ['GET'])]
    public function champions(): Response{
        // 1) On récupère les paramètres
        
        $session = $this->clientManager->getParams();

        // 1.1) Si nos paramètres ne sont pas défini alors on les définis via la redirection
        if(!$session['param']){
            return $this->redirectToRoute('app_champions_redirect');
        }


        try {
            $data = $this->championManager->paginate($session['version'], $session['lang'], $session['itemPerPage'] > 20 ? 20 : $session['itemPerPage'], $session['numPage']);
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
        /* dd($data); */
        return $this->render('champion/liste.html.twig', [
            'champions' => $data['champions'],
            'images' => $data['images'],
            'meta' => $data['meta'],
            'client' => ClientData::fromServices($this->versionManager, $this->clientManager),
        ]);
    }



    #[Route('/champion_redirect/{name}', name: 'app_champion_redirect', methods: ['GET'])]
    public function champion_redirect(string $name): Response
    {
        // On récupère les informations en session
        $session = $this->clientManager->getSession();

        // On les ajoute en paramètre dans l'URL de la page détail
        return $this->redirectToRoute('app_champion', [
            'name'    => $name,
            'version' => $session['version'],
            'lang'    => $session['lang'],
        ]);
    }

    /**
     * Affiche le détail complet d’un champion spécifique.
     *
     * Récupère les informations détaillées d’un champion (nom, titre, stats, rôles, image, lore)
     * en fonction du nom passé dans l’URL. En cas d’absence de données sur la version/langue courante,
     * l’utilisateur est redirigé vers la page de configuration.
     *
     * @param string $name Nom du champion (identifiant interne de Data Dragon).
     *
     * @return Response Page détaillée du champion demandé.
     *
     * @throws \RuntimeException Si le champion n’est pas trouvé pour la version/langue spécifiée.
     * @throws \Throwable Si une erreur survient pendant la récupération des données ou des images.
     */
    #[Route('/champion/{name}', name: 'app_champion', methods: ['GET'])]
    public function champion(string $name): Response{

        $session = $this->clientManager->getParams(['version', 'lang']);

        // Si pas de paramètres valides → redirection
        if (!$session['param']) {
            return $this->redirectToRoute('app_summoner_redirect', ['name' => $name]);
        }

        try {
            $image = $this->championManager->getImage($name . '.png', $session['version'], [], false, $session['lang']);
            $champion = $this->championManager->getByName($name, $session['version'], $session['lang']);
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
        /* dd($champion, $image); */
        return $this->render('champion/detail.html.twig', [
            'champion' => $champion,
            'image'    => $image,
            'client' => ClientData::fromServices($this->versionManager, $this->clientManager),
        ]);
    }

    /**
     * Endpoint API pour la recherche de champions.
     *
     * Permet la recherche asynchrone (AJAX) de champions à partir d’un nom partiel.
     * Retourne un JSON contenant les id, noms et images des champions correspondants.
     *
     * @param string $name Nom ou partie du nom du champion à rechercher.
     *
     * @return JsonResponse Tableau JSON contenant les résultats de la recherche.
     *
     * @throws \Throwable Si la récupération des données échoue ou si la requête est invalide.
     */
    #[Route('/api/champions/search/{name}', name: 'api_champions_search', methods: ['GET'])]
    public function searchChampionsApi(string $name): JsonResponse
    {
        $session = $this->clientManager->getSession();
        try {
            $champions = $this->championManager->searchByName($name, $session['version'], $session['lang'], 20);
        } catch (\Throwable $e) {
            return $this->json( sprintf(
                "Donnés absente sur la version %s et la langue %s Message --> %s",
                $session['version'] ?? 'n/a',
                $session['lang'] ?? 'n/a',
                $e->getMessage()
            ));
        }

        if (empty($champions)) {
            return $this->json([]);
        }

        $images = $this->championManager->getImages($session['version'], $session['lang'], false, $champions);
        
        // Filtrer uniquement id, name et image

        $final = array_map(function ($champion, $image) {
            $id = $champion['id'] ?? null;
            return [
                'id'    => $id,
                'name'  => $champion['name'] ?? '',
                'image' => $image,
            ];
        }, $champions, $images);

        return $this->json($final);
    }
}
