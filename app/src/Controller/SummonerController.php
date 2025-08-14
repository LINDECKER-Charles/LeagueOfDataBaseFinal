<?php
declare(strict_types=1);

namespace App\Controller;

use App\Dto\ClientData;
use App\Service\ClientManager;
use App\Service\VersionManager;
use App\Service\SummonerManager;
use Symfony\Component\HttpFoundation\Response;
use Symfony\Component\Routing\Annotation\Route;
use Symfony\Component\HttpFoundation\RequestStack;
use Symfony\Bundle\FrameworkBundle\Controller\AbstractController;

final class SummonerController extends AbstractController
{
    public function __construct(
        private readonly SummonerManager $summoners,
        private readonly ClientManager $client,
        private readonly VersionManager $versionManager, 
        private readonly ClientManager $clientManager,
        private readonly RequestStack $requestStack,
    ) {}

    #[Route('/summoners_redirect', name: 'app_summoners_redirect', methods: ['GET'])]
    public function summoners_redirect(): Response{
        // On recupere les informations en session
        $session = $this->client->getSession();
        // On les ajoutes en parametre dans l URL
        return $this->redirectToRoute('app_summoners', [
            'version' => $session['version'],
            'lang' => $session['lang'],
        ]);
    }

    #[Route('/summoners', name: 'app_summoners', methods: ['GET'])]
    public function summoners(): Response
    {
        // 1) On récupère les paramètres
        $session = $this->client->getParams();
        // 1.1) Si nos paramètres ne sont pas défini alors on les définis via la redirection
        if(!$session['param']){
            return $this->redirectToRoute('app_summoners_redirect');
        }
        // 2) Verification de securite mais ne devrais pas etre appeler
        if (!$session['version'] || !$session['lang']) {
            // Ultra simple: on refuse si on n’a rien de fiable
            return new Response('Langue/version introuvables. Définissez vos préférences.', 400);
        }
        
        try {
            $summoners = $this->summoners->getSummonersOrderAndParsed($session['version'], $session['lang']);
            $images    = $this->summoners->getSummonersImages($session['version'], $session['lang'], false);
            /* dd($images, $summoners); */
        } catch (\Throwable $e) {
            $this->requestStack->getSession()->getFlashBag()->clear();
            $this->addFlash('error', sprintf(
                "Donnés absente de l\'API sur la version %s%s%s",
                $session['version'] ?? 'n/a',
                PHP_EOL,
                $e->getMessage()
            ));
            return $this->redirectToRoute('app_setup');
        }

        return $this->render('summoner/liste.html.twig', [
            'summoners' => $summoners,
            'images'    => $images,
            'client' => ClientData::fromServices($this->versionManager, $this->clientManager),
        ]);
    }
}
