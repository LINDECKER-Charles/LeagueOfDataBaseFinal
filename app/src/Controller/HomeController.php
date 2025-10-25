<?php

namespace App\Controller;

use App\Dto\ClientData;

use App\Service\API\ChampionManager;
use App\Service\API\ItemManager;
use App\Service\API\SummonerManager;
use App\Service\Client\ClientManager;
use App\Service\Client\VersionManager;
use App\Service\Tools\UrlGenerator;
use Symfony\Component\HttpFoundation\Request;
use Symfony\Component\HttpFoundation\Response;
use Symfony\Component\Routing\Annotation\Route;
use Symfony\Component\HttpFoundation\RedirectResponse;
use Symfony\Bundle\FrameworkBundle\Controller\AbstractController;

class HomeController extends AbstractController
{

    public function __construct(
        private readonly VersionManager $versionManager, 
        private readonly ClientManager $clientManager,
        private readonly UrlGenerator $urlGenerator,
        private readonly ItemManager $itemManager,
        private readonly SummonerManager $summonerManager,
        private readonly ChampionManager $championManager,
    ){}

    /**
     * Affiche la page « Work in progress ».
     *
     * Prépare les données transverses du client (versions, langues, libellés,
     * locale courante, préférences de session) via ClientData::fromServices()
     * puis rend le template Twig `home/working.html.twig`.
     *
     * @see ClientData::fromServices()
     *
     * @return \Symfony\Component\HttpFoundation\Response Réponse HTML.
     * @throws \Twig\Error\Error Si le template ne peut pas être rendu.
     */
    #[Route('/working-progress', name: 'app_working')]
    public function working(): Response
    {
        return $this->render('home/working.html.twig', [
            'client' => ClientData::fromServices($this->versionManager, $this->clientManager),
        ]);
    }

    /**
     * Page de configuration initiale (version/langue).
     *
     * Construit un view-model minimal via {@see App\Dto\ClientData::fromServices()}
     * et l’injecte dans le template `home/setupPage.html.twig` sous la clé `client`.
     *
     * Variables Twig exposées :
     *  - client.versions        (string[])
     *  - client.languages       (string[])
     *  - client.languageLabels  (array<string,string>)
     *  - client.currentLocale   (string)
     *  - client.session         (array{locale:?string, version:?string})
     *
     * @route  /setup  name=app_setup
     *
     * @return \Symfony\Component\HttpFoundation\Response Réponse HTML de la page setup.
     */
    #[Route('/', name: 'app_setup')]
    public function setup(): Response
    {
        return $this->render('home/setupPage.html.twig', [
            'client' => ClientData::fromServices($this->versionManager, $this->clientManager),
            'typeV' => 'LOL',
        ]);
    }

    /**
     * Traitement du formulaire de sélection version/langue.
     *
     * Étapes :
     *  1) Valide le token CSRF (`_token` avec l'ID 'setup_form').
     *  2) Récupère les champs POST : `langue` (string), `version` (string), `remember` (bool).
     *  3) Valide le couple (version, langue) via {@see App\Service\VersionManager::validateSelection()}.
     *     - En cas d’erreurs : clear du FlashBag, ajout des messages d’erreur, redirection vers la page d’origine (Referer) avec fallback `/setup`.
     *  4) En cas de succès :
     *     - Si `remember` = true : crée un cookie signé via {@see App\Service\ClientManager::makeRememberCookie()}.
     *       Sinon : envoie un cookie d’effacement via {@see App\Service\ClientManager::makeForgetCookie()}.
     *     - Écrit les préférences en session via {@see App\Service\ClientManager::setLocaleInSession()} et {@see App\Service\ClientManager::setVersionInSession()}.
     *     - Clear du FlashBag puis ajout d’un flash 'success'.
     *     - Redirection vers la page d’origine (Referer) avec fallback `/setup`.
     *
     * Notes sécurité :
     *  - Le Referer est contrôlé pour rester sur le même host ; sinon fallback `/setup`.
     *  - Le cookie “remember” assure l’intégrité via HMAC mais pas la confidentialité.
     *
     * @route   /setup-submit  name=app_setup_save  methods=POST
     *
     * @param  \Symfony\Component\HttpFoundation\Request $request Requête HTTP contenant le formulaire.
     * @return \Symfony\Component\HttpFoundation\RedirectResponse  Redirection vers la page d’origine ou `/setup`.
     */
    #[Route('/setup-submit', name: 'app_setup_save', methods: ['POST'])]
    public function save(Request $request): RedirectResponse
    {
        // On recupere les donnees
        (string) $language = (string) $request->request->get('langue', '');
        (string) $version  = (string) $request->request->get('version', '');
        (bool) $remember = (bool) $request->request->getBoolean('remember');

        //On les valides
        $report = $this->versionManager->validateSelection($version, $language);

        $backUrl = $this->urlGenerator->generateBackUrl();

        if (!$report['ok']) {
            $request->getSession()?->getFlashBag()->clear();
            foreach ($report['errors'] as $field => $msg) {
                $this->addFlash('error', sprintf('%s: %s', ucfirst($field), $msg));
            }
            return $this->redirect($backUrl);
        }

        // 2) Réécrit la query (sauf sur / et /working-progress), en nettoyant l’existant
        $backUrl = $this->urlGenerator->rewriteQueryParams(
            $backUrl,
            overrides: ['version' => $version, 'lang' => $language],
            removeKeys: ['version', 'lang']
        );


        // Succès, on enregistre le cookie si l'utilisateur le demande et on save les preference dans la session
        $response = $this->redirect($backUrl);

        if ($remember) {
            $response->headers->setCookie(
                $this->clientManager->makeRememberCookie($language, $version, 7)
            );
        } else {
            // s'assurer qu’un ancien cookie est supprimé
            $response->headers->setCookie($this->clientManager->makeForgetCookie());
        }

        $this->clientManager->setLocaleInSession($language);
        $this->clientManager->setVersionInSession($version);


        // Petit feedback facultatif
        $request->getSession()?->getFlashBag()->clear();
        $this->addFlash('success', 'Preferences saved');
        return $response;
    }

    /**
     * Page d'accueil de l'application.
     *
     * Comportement :
     * 1. Récupère les informations de session (version et langue courantes)
     *    via {@see ClientManager::getSession()}.
     * 2. Charge un aperçu limité de données :
     *    - 4 sorts d'invocateur (page 1) via {@see SummonerManager::paginate()}.
     *    - 4 items (page 1) via {@see ItemManager::paginate()}.
     * 3. Construit un objet {@see ClientData} pour exposer les données globales
     *    (versions disponibles, langues, locale courante, etc.).
     * 4. Rend le template Twig `home/home.html.twig` avec les données préparées.
     *
     * Variables injectées dans la vue :
     * - client    : objet {@see ClientData} contenant les métadonnées de version/langue.
     * - summoners : tableau paginé contenant 4 sorts d'invocateur + images + meta.
     * - items     : tableau paginé contenant 4 items + images + meta.
     *
     * @see SummonerManager::paginate()
     * @see ItemManager::paginate()
     * @see ClientData::fromServices()
     *
     * @return Response Page HTML de la home affichant les aperçus de Summoners et Items.
     */
    #[Route('/home', name: 'app_home')]
    public function home(): Response{
        $session = $this->clientManager->getSession();
        $summoners = $this->summonerManager->paginate($session['version'], $session['lang'], 4, 1);
        $items = $this->itemManager->paginate($session['version'], $session['lang'], 4, 1);
        $champions = $this->championManager->paginate($session['version'], $session['lang'], 4, 1);
        return $this->render('home/home.html.twig', [
            'client' => ClientData::fromServices($this->versionManager, $this->clientManager),
            'summoners' => $summoners,
            'items' => $items,
            'champions' => $champions,
        ]);
    }
}
