<?php
declare(strict_types=1);

namespace App\Controller;

use App\Dto\ClientData;
use App\Service\API\RuneManager;
use App\Service\Client\ClientManager;
use App\Service\Client\VersionManager;
use Symfony\Component\HttpFoundation\Response;
use Symfony\Component\Routing\Annotation\Route;
use Symfony\Component\HttpFoundation\JsonResponse;
use Symfony\Component\HttpFoundation\RequestStack;
use Symfony\Bundle\FrameworkBundle\Controller\AbstractController;

final class RuneController extends AbstractController{
    public function __construct(
        private readonly VersionManager $versionManager, 
        private readonly ClientManager $clientManager,
        private readonly RequestStack $requestStack,
        private readonly RuneManager $runeManager,
    ) {}
    #[Route('/runes_redirect/{numpage}/{itemperpage}', 
        name: 'app_runes_redirect', 
        methods: ['GET'], 
        defaults: ['numpage' => 1,'itemperpage' => 8]
    )]
    public function runes_redirect(int $numpage, int $itemperpage): Response{
        // On recupere les informations en session

        $session = $this->clientManager->getSession();
            
        // On les ajoutes en parametre dans l URL
        return $this->redirectToRoute('app_runes', [
                'version' => $session['version'],
                'lang' => $session['lang'],
                'numpage' => $numpage,
                'itemperpage' => $itemperpage > 20 ? 20 : $itemperpage,
            ]);
    }

    #[Route('/runes', name: 'app_runes', methods: ['GET'])]
    public function runes(): Response{
        // 1) On récupère les paramètres
        
        $session = $this->clientManager->getParams();

        // 1.1) Si nos paramètres ne sont pas défini alors on les définis via la redirection
        if(!$session['param']){
            return $this->redirectToRoute('app_runes_redirect');
        }
        /* dd($this->runeManager->getImages($session['version'], $session['lang']), $this->runeManager->getData($session['version'], $session['lang'])); */
        try {
            $data = $this->runeManager->paginate($session['version'], $session['lang'], $session['itemPerPage'] > 20 ? 20 : $session['itemPerPage'], $session['numPage']);
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

        return $this->render('rune/liste.html.twig', [
            'runesReforgeds' => $data['runesReforgeds'],
            'images' => $data['images'],
            'meta' => $data['meta'],
            'client' => ClientData::fromServices($this->versionManager, $this->clientManager),
        ]);
    }

    #[Route('/rune_redirect/{name}', name: 'app_rune_redirect', methods: ['GET'])]
    public function rune_redirect(string $name): Response
    {
        // On récupère les informations en session
        $session = $this->clientManager->getSession();
        /* dd(); */
        // On les ajoute en paramètre dans l'URL de la page détail
        return $this->redirectToRoute('app_rune', [
            'name'    => $name,
            'version' => $session['version'],
            'lang'    => $session['lang'],
        ]);
    }

    #[Route('/rune/{name}', name: 'app_rune', methods: ['GET'])]
    public function rune(string $name): Response{

        $session = $this->clientManager->getParams(['version', 'lang']);

        // Si pas de paramètres valides → redirection
        if (!$session['param']) {
            return $this->redirectToRoute('app_summoner_redirect', ['name' => $name]);
        }

        try {
            $rune = $this->runeManager->getByName($name, $session['version'], $session['lang']);
            
            $images = $this->runeManager->getImages($session['version'], $session['lang'], false, [$rune]);
            
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
        /* dd($rune, $images); */
        return $this->render('rune/detail.html.twig', [
            'rune' => $rune,
            'images' => $images,
            'client' => ClientData::fromServices($this->versionManager, $this->clientManager),
        ]);
    }
}
