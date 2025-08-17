<?php
declare(strict_types=1);

namespace App\Controller;

use App\Dto\ClientData;
use App\Service\RuneManager;
use App\Service\ClientManager;
use App\Service\VersionManager;
use Symfony\Component\HttpFoundation\Response;
use Symfony\Component\Routing\Annotation\Route;
use Symfony\Component\HttpFoundation\JsonResponse;
use Symfony\Component\HttpFoundation\RequestStack;
use Symfony\Bundle\FrameworkBundle\Controller\AbstractController;

final class RuneController extends AbstractController{
    public function __construct(
        private readonly ClientManager $client,
        private readonly VersionManager $versionManager, 
        private readonly ClientManager $clientManager,
        private readonly RequestStack $requestStack,
        private readonly RuneManager $runeManager,
    ) {}

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


    #[Route('/runes', name: 'app_runes', methods: ['GET'])]
    public function runes(): Response{

        $session = $this->client->getSession();

        $runes = $this->runeManager->getRunes($session['version'], $session['lang']);
        $runesDomination = $this->runeManager->getRuneByName('Domination', $session['version'], $session['lang']);
        $imgRunesDomination = $this->runeManager->getRuneImage($runesDomination['icon'], $session['version'], [], false, $session['lang']);
        dd($runes, $runesDomination, $imgRunesDomination);
        // 1) On récupère les paramètres
        $session = $this->client->getParams();

        // 1.1) Si nos paramètres ne sont pas défini alors on les définis via la redirection
        if(!$session['param']){
            /* dd($session); */
            return $this->redirectToRoute('app_items_redirect');
        }

        try {
            $data = $this->itemManager->paginateItems($session['version'], $session['lang'], $session['itemPerPage'] > 20 ? 20 : $session['itemPerPage'], $session['numPage']);
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
}
