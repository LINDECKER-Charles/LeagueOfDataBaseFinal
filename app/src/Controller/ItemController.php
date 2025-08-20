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
     * Redirige vers la liste des items ('app_items') en injectant tous les paramètres requis.
     *
     * Paramètres injectés dans la route cible :
     * - version (string)   : récupérée depuis la session client ($session['version']).
     * - lang (string)      : récupérée depuis la session client ($session['lang']).
     * - numpage (int)      : numéro de page tel que reçu dans l’URL courante
     *                        (/objects_redirect/{numpage}/{itemperpage}) — défaut = 1 (déclaré sur l’attribut Route).
     * - itemperpage (int)  : nombre d’items par page tel que reçu dans l’URL, borné à 20
     *                        (si > 20, alors 20) — défaut = 8 (déclaré sur l’attribut Route).
     *
     * Comportement :
     * - Lit 'version' et 'lang' depuis la session.
     * - Reprend 'numpage' et 'itemperpage' depuis l’URL, applique un plafond à 20.
     * - Effectue une redirection HTTP 302 vers la route 'app_items' avec ces paramètres.
     *
     * Préconditions :
     * - La session doit contenir les clés 'version' et 'lang'. À défaut, la route cible
     *   devra gérer le cas (valeurs par défaut ou erreur contrôlée).
     *
     * @param int $numpage     Numéro de page demandé (≥ 1).
     * @param int $itemperpage Nombre d’items par page (plafonné à 20).
     *
     * @return Response Redirection vers 'app_items' avec version, lang, numpage et itemperpage.
     */
    #[Route('/objects_redirect/{numpage}/{itemperpage}', 
        name: 'app_items_redirect', 
        methods: ['GET'], 
        defaults: ['numpage' => 1,'itemperpage' => 8]
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
    #[Route('/objects', name: 'app_items', methods: ['GET'])]
    public function objects(): Response{
        // 1) On récupère les paramètres
        
        $session = $this->client->getParams();

        // 1.1) Si nos paramètres ne sont pas défini alors on les définis via la redirection
        if(!$session['param']){
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
            'images' => $data['images'],
            'meta' => $data['meta'],
            'client' => ClientData::fromServices($this->versionManager, $this->clientManager),
        ]);
    }

    
    /**
     * Redirige vers la page de détail d’un item en injectant les paramètres nécessaires.
     *
     * - Récupère les informations de version et de langue depuis la session utilisateur.
     * - Construit l’URL de la route 'app_item' en y injectant :
     *   - name    : l’identifiant ou le nom de l’item passé dans l’URL courante.
     *   - version : la version de League of Legends stockée en session.
     *   - lang    : le code de langue stocké en session.
     *
     * @param string $name Identifiant ou nom de l’item (clé du JSON, ex. "1001").
     *
     * @return Response Redirection HTTP vers la route 'app_item' avec name, version et langue.
     */
    #[Route('/object_redirect/{name}', name: 'app_item_redirect', methods: ['GET'])]
    public function object_redirect(string $name): Response
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

    /**
     * Affiche la page de détail d’un item spécifique.
     *
     * Comportement :
     * - Récupère les paramètres de session nécessaires ('version', 'lang').
     * - Si ces paramètres ne sont pas définis, redirige vers la route
     *   'app_item_redirect' pour les initialiser correctement (avec le nom d’item).
     * - Sinon, tente de récupérer :
     *   - l’image de l’item via {@see ItemManager::getImage()}.
     *   - les données de l’item via {@see ItemManager::getByName()}.
     * - En cas d’échec (item introuvable ou données absentes pour version/langue),
     *   vide les messages flash, ajoute un flash "error" avec le détail de l’erreur,
     *   puis redirige vers la route 'app_setup'.
     * - Rend ensuite le template Twig `item/detail.html.twig` avec :
     *   - item   : données de l’item (tableau associatif).
     *   - image  : chemin relatif de l’image de l’item.
     *   - client : objet {@see ClientData} construit depuis versionManager et clientManager.
     *
     * @param string $name Identifiant de l’item (clé du JSON, ex. "1001" pour les bottes).
     *
     * @return Response Page HTML affichant le détail d’un item,
     *                  ou redirection si paramètres absents/erreur.
     */
    #[Route('/object/{name}', name: 'app_item', methods: ['GET'])]
    public function object(string $name): Response{

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

    /**
     * API : Recherche des items par nom et retourne un JSON simplifié.
     *
     * - Récupère la version et la langue en session.
     * - Recherche jusqu’à 20 items correspondant au terme donné via
     *   {@see ItemManager::searchByName()} (recherche insensible à la casse).
     * - Récupère les images correspondantes via {@see ItemManager::getImages()}.
     * - Construit une réponse JSON simplifiée contenant uniquement :
     *   - id    : identifiant de l’item (clé du JSON Data Dragon).
     *   - name  : nom affiché de l’item.
     *   - image : chemin relatif de l’image (ou null si indisponible).
     *
     * En cas d’erreur (version/langue non disponibles, données manquantes),
     * retourne une réponse JSON contenant un message d’erreur formaté.
     *
     * @param string $name Terme de recherche (min. 2, max. 50 caractères).
     *
     * @return JsonResponse Réponse JSON contenant :
     *                      - tableau de résultats simplifiés en cas de succès,
     *                      - ou chaîne de message d’erreur en cas d’exception.
     */
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

        return $this->json($final);
    }
}
