<?php
declare(strict_types=1);

namespace App\Controller;

use App\Dto\ClientData;
use App\Service\ClientManager;
use App\Service\VersionManager;
use App\Service\SummonerManager;
use Symfony\Component\HttpFoundation\Response;
use Symfony\Component\Routing\Annotation\Route;
use Symfony\Bundle\FrameworkBundle\Controller\AbstractController;

final class SummonerController extends AbstractController
{
    public function __construct(
        private readonly SummonerManager $summoners,
        private readonly ClientManager $client,
        private readonly VersionManager $versionManager, 
        private readonly ClientManager $clientManager
    ) {}

    #[Route('/summoners', name: 'app_summoners_index', methods: ['GET'])]
    public function index(): Response
    {
        // 1) Préférences depuis la session (ou cookie signé)
        $session = $this->client->getSession();
        if (!$session['version'] || !$session['version']) {
            // Ultra simple: on refuse si on n’a rien de fiable
            return new Response('Langue/version introuvables. Définissez vos préférences.', 400);
        }

        $summoners = $this->summoners->getSummonersParsed($session['version'], $session['lang']);
        $images = $this->summoners->fetchSummonerImages($session['version'], $session['lang'], false);

        return $this->render('sumonner/liste.html.twig', [
            'summoners' => $summoners,
            'images'    => $images,
            'client' => ClientData::fromServices($this->versionManager, $this->clientManager),
        ]);
    }
}
