<?php
declare(strict_types=1);

namespace App\Controller;

use App\Dto\ClientData;

use App\Service\API\ChampionManager;
use App\Service\API\ItemManager;
use App\Service\API\RuneManager;
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
        private readonly RuneManager $runeManager,
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
     * Traitement du formulaire de sélection version/langue.
     *
     * Étapes :
     *  1) Valide le token CSRF (`_token` avec l'ID 'setup_form').
     *  2) Récupère les champs POST : `langue` (string), `version` (string), `remember` (bool).
     *  3) Valide le couple (version, langue) via {@see App\Service\VersionManager::validateSelection()}.
     *     - En cas d’erreurs : clear du FlashBag, ajout des messages d’erreur, redirection vers la page d’origine (Referer) avec fallback la home.
     *  4) En cas de succès :
     *     - Si `remember` = true : crée un cookie signé via {@see App\Service\ClientManager::makeRememberCookie()}.
     *       Sinon : envoie un cookie d’effacement via {@see App\Service\ClientManager::makeForgetCookie()}.
     *     - Écrit les préférences en session via {@see App\Service\ClientManager::setLocaleInSession()} et {@see App\Service\ClientManager::setVersionInSession()}.
     *     - Clear du FlashBag puis ajout d’un flash 'success'.
     *     - Redirection vers la page d’origine (Referer) avec fallback la home.
     *
     * Notes sécurité :
     *  - Le Referer est contrôlé pour rester sur le même host ; sinon fallback la home.
     *  - Le cookie “remember” assure l’intégrité via HMAC mais pas la confidentialité.
     *
     * @route   /setup-submit  name=app_setup_save  methods=POST
     *
     * @param  \Symfony\Component\HttpFoundation\Request $request Requête HTTP contenant le formulaire.
     * @return \Symfony\Component\HttpFoundation\RedirectResponse  Redirection vers la page d’origine ou la home.
     */
    #[Route('/setup-submit', name: 'app_setup_save', methods: ['POST'])]
    public function save(Request $request): RedirectResponse
    {
        // On recupere les donnees
        $language = (string) $request->request->get('langue', '');
        $version  = (string) $request->request->get('version', '');
        $remember = $request->request->getBoolean('remember');

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

        // 2) Réécrit la query (sauf sur /working-progress), en nettoyant l’existant
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
    #[Route('/', name: 'app_home')]
    public function home(): Response{
        $session = $this->clientManager->getSession();
        $version = $session['version'];
        $lang    = $session['lang'];

        return $this->render('home/home.html.twig', [
            'client'    => ClientData::fromServices($this->versionManager, $this->clientManager),
            'champions' => $this->preview('champion',      fn () => $this->championManager->paginate($version, $lang, 4, 1)),
            'items'     => $this->preview('item',          fn () => $this->itemManager->paginate($version, $lang, 4, 1)),
            'summoners' => $this->preview('summoner',      fn () => $this->summonerManager->paginate($version, $lang, 4, 1)),
            'runes'     => $this->preview('runesReforged', fn () => $this->runeManager->paginate($version, $lang, 4, 1)),
        ]);
    }

    /**
     * Ancienne URL de la home. Redirection permanente : des PWA installées ont
     * `/home` en start_url (manifest mis en cache) et des liens externes la
     * référencent encore.
     */
    #[Route('/home', name: 'app_home_legacy')]
    public function homeLegacy(): RedirectResponse
    {
        return $this->redirectToRoute('app_home', status: Response::HTTP_MOVED_PERMANENTLY);
    }

    /**
     * Rend une preview de la home isolée : l'échec d'une ressource (panne
     * transitoire de l'upstream) renvoie une section vide au lieu de faire tomber
     * toute la page. L'absence légitime de données est déjà neutralisée en amont
     * (jeu vide côté managers) ; ce garde-fou couvre les erreurs que la couche
     * données propage volontairement. La forme vide conserve les clés attendues
     * par le template (`<type>s`, `images`, `meta`) pour rester compatible
     * strict_variables.
     *
     * @param callable():array<mixed> $fetch
     * @return array<mixed>
     */
    private function preview(string $type, callable $fetch): array
    {
        try {
            return $fetch();
        } catch (\Throwable) {
            return [$type . 's' => [], 'images' => [], 'meta' => []];
        }
    }
}
