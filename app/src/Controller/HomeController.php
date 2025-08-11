<?php

namespace App\Controller;

use App\Dto\ClientData;
use App\Service\ClientManager;
use App\Service\VersionManager;
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
    ){}

    /**
     * Page de test/sandbox.
     *
     * Rend le template `home/test.html.twig` avec un view-model `client`
     * (voir {@see App\Dto\ClientData::fromServices()}) pour disposer des
     * versions/langues/labels, de la locale courante et des préférences session.
     *
     * @route  /test  name=app_test
     *
     * @return \Symfony\Component\HttpFoundation\Response Réponse HTML de la page de test.
     */
    #[Route('/test', name: 'app_test')]
    public function index(): Response
    {
        return $this->render('home/test.html.twig', [
            'client' => ClientData::fromServices($this->versionManager, $this->clientManager),
        ]);
    }

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
    #[Route('/working-progess', name: 'app_working')]
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
            'client' => ClientData::fromServices($this->versionManager, $this->clientManager)
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
        // On valide le token csrf
/*         if (!$this->isCsrfTokenValid('setup_form', (string) $request->request->get('_token'))) {
            $request->getSession()?->getFlashBag()->clear();
            $this->addFlash('error', 'CSRF token invalide.');
            return $this->redirectToRoute('app_setup');
        } */

        // On recupere les donnees
        $language = (string) $request->request->get('langue', '');
        $version  = (string) $request->request->get('version', '');
        $remember = $request->request->getBoolean('remember');

        //On les valides
        $report = $this->versionManager->validateSelection($version, $language);

        $backUrl = $request->headers->get('referer') ?: $this->generateUrl('app_setup');
        // (optionnel) sécurité basique: ne redirige que vers le même host
        if (!str_starts_with($backUrl, $request->getSchemeAndHttpHost())) {
            $backUrl = $this->generateUrl('app_setup');
        }

        if (!$report['ok']) {
            $request->getSession()?->getFlashBag()->clear();
            foreach ($report['errors'] as $field => $msg) {
                $this->addFlash('error', sprintf('%s: %s', ucfirst($field), $msg));
            }
            return $this->redirect($backUrl);
        }

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
        $this->addFlash('success', 'Préférences reçues 👍');
        return $response;
    }
}
