<?php
declare(strict_types=1);

namespace App\Controller;

use App\Dto\ClientData;
use App\Service\API\ChampionManager;
use App\Service\Client\ClientManager;
use App\Service\Client\VersionManager;
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
     * Affiche la liste des items avec pagination, version et langue définies en session.
     *
     * Comportement :
     * 1. Récupère les paramètres depuis la session via {@see Client::getParams()} :
     *    - version (string)      : version de League of Legends en cours.
     *    - lang (string)         : code de langue (ex. "fr_FR", "en_US").
     *    - numPage (int)         : numéro de la page courante.
     *    - itemPerPage (int)     : nombre d’items par page.
     *    - param (bool)          : indicateur de validité des paramètres.
     *
     * 2. Si les paramètres ne sont pas définis (`param = false`), redirige vers la route
     *    'app_items_redirect' afin de forcer leur initialisation.
     *
     * 3. Sinon, tente de récupérer les items via {@see ItemManager::paginate()} :
     *    - Applique un plafond de 20 items par page.
     *    - Récupère les données paginées + images + métadonnées.
     *
     * 4. En cas d’erreur (ex. données absentes pour la version/langue choisies),
     *    vide les messages flash existants, ajoute un flash "error" avec les détails,
     *    puis redirige vers la route 'app_setup'.
     *
     * 5. Rend le template Twig `item/liste.html.twig` avec :
     *    - items   : sous-ensemble d’items pour la page courante.
     *    - images  : chemins relatifs des images d’items.
     *    - meta    : informations de pagination (page courante, nb pages, total, etc.).
     *    - client  : objet {@see ClientData} construit depuis les services versionManager et clientManager.
     *
     * @return Response Page HTML affichant la liste paginée des items,
     *                  ou redirection vers une autre route en cas de paramètres absents/erreur.
     */
    #[Route('/champions', name: 'app_champions', methods: ['GET'])]
    public function champions(): Response{
        // 1) On récupère les paramètres
        
        $session = $this->clientManager->getParams();

        // 1.1) Si nos paramètres ne sont pas défini alors on les définis via la redirection
        if(!$session['param']){
            return $this->redirectToRoute('app_champions_redirect');
        }

/*         $data = $this->championManager->getData($session['version'], $session['lang']);
        dd($data); */

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
}
