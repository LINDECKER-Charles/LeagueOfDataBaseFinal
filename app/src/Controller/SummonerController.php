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

    /**
    * Redirige vers la route des invocateurs (/summoners) en ajoutant
    * les paramètres `version` et `lang` depuis la session de l'utilisateur.
    *
    * Cette route sert à garantir que les paramètres nécessaires sont
    * présents dans l'URL avant de charger la page principale.
    *
    * @return Response
    */
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

    /**
     * Affiche la liste des sorts d'invocateur (Summoner Spells) pour une version et une langue données.
     *
     * 1. Récupère les paramètres `version` et `lang` depuis la requête.
     * 2. Si les paramètres sont absents ou invalides, redirige vers `app_summoners_redirect`
     *    pour les injecter dans l'URL depuis la session.
     * 3. Charge et prépare les données des invocateurs et leurs images via le service Summoners.
     * 4. Rend la vue Twig avec les données récupérées.
     *
     * En cas d'erreur (ex. données manquantes sur l'API), affiche un message flash et
     * redirige vers la page de configuration.
     *
     * @return Response
     */
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

    /**
     * Redirige vers la page de détail d'un sort d'invocateur
     * en ajoutant automatiquement la version et la langue depuis la session.
     *
     * - Récupère la version et la langue de l'utilisateur via {@see ClientManager::getSession()}.
     * - Construit l'URL de la route `app_summoner` en y injectant :
     *     - le nom du sort d'invocateur (`name`) en paramètre de chemin,
     *     - la version et la langue en paramètres de requête.
     *
     * @param string $name Nom interne du sort d'invocateur (ex: "SummonerBarrier").
     *
     * @return Response Redirection HTTP vers la route `app_summoner`.
     */
    #[Route('/summoner_redirect/{name}', name: 'app_summoner_redirect', methods: ['GET'])]
    public function summoner_redirect(string $name): Response
    {
        // On récupère les informations en session
        $session = $this->clientManager->getSession();

        // On les ajoute en paramètre dans l'URL de la page détail
        return $this->redirectToRoute('app_summoner', [
            'name'    => $name,
            'version' => $session['version'],
            'lang'    => $session['lang'],
        ]);
    }

    
    /**
     * Affiche la page de détail d'un sort d'invocateur spécifique.
     *
     * - Vérifie que la version et la langue sont présentes et valides dans l'URL ou la requête.
     * - Si les paramètres sont manquants ou invalides, redirige vers `app_summoner_redirect`
     *   pour les injecter automatiquement depuis la session.
     * - Récupère l'image du sort et ses informations complètes via les services Summoners.
     * - Rend la vue `summoner/detail.html.twig` avec les données.
     *
     * @param string $name Nom interne du sort d'invocateur (ex: "SummonerBarrier").
     *
     * @return Response Page de détail du sort d'invocateur.
     *
     * @throws \RuntimeException Si les données du sort ne peuvent pas être récupérées.
     */
    #[Route('/summoner/{name}', name: 'app_summoner', methods: ['GET'])]
    public function summoner(string $name): Response{

        $session = $this->clientManager->getParams();

        // Si pas de paramètres valides → redirection
        if (!$session['param']) {
            return $this->redirectToRoute('app_summoner_redirect', ['name' => $name]);
        }

        $image = $this->summoners->getSummonerImage($name . '.png', $session['version'], [], false, $session['lang']);
        $summoner = $this->summoners->getSummonerByName($name, $session['version'], $session['lang']);
        /* dd($image, $summoner); */


        return $this->render('summoner/detail.html.twig', [
            'summoner' => $summoner,
            'image'    => $image,
            'client' => ClientData::fromServices($this->versionManager, $this->clientManager),
        ]);
    }
}
