<?php

namespace App\Controller;

use App\Service\ClientManager;
use App\Service\VersionManager;
use Symfony\Component\HttpFoundation\Request;
use Symfony\Component\HttpFoundation\Response;
use Symfony\Component\Routing\Annotation\Route;
use Symfony\Component\HttpFoundation\RedirectResponse;
use Symfony\Bundle\FrameworkBundle\Controller\AbstractController;

class HomeController extends AbstractController
{

    private VersionManager $versionManager;
    private ClientManager $clientManager;

    public function __construct(VersionManager $versionManager, ClientManager $clientManager)
    {
        $this->versionManager = $versionManager;
        $this->clientManager  = $clientManager;
    }


    #[Route('/test', name: 'app_test')]
    public function index(): Response
    {
        return $this->render('home/test.html.twig');
    }

    #[Route('/setup', name: 'app_setup')]
    public function setup(): Response
    {
        return $this->render('home/setupPage.html.twig', [
            'versions' => $this->versionManager->getVersions(),
            'languages' => $this->versionManager->getLanguages(),
            'languageLabels' => $this->versionManager->getLanguageLabels(),
            'currentLocale'  => $this->clientManager->getLangue(),
            'session' => $this->clientManager->getOrHydratePreferences(),
        ]);
    }

    #[Route('/setup-submit', name: 'app_setup_save', methods: ['POST'])]
    public function save(Request $request): RedirectResponse
    {
        if (!$this->isCsrfTokenValid('setup_form', (string) $request->request->get('_token'))) {
            $this->addFlash('error', 'CSRF token invalide.');
            return $this->redirectToRoute('app_setup');
        }

        $language = (string) $request->request->get('langue', '');
        $version  = (string) $request->request->get('version', '');
        $remember = $request->request->getBoolean('remember');
        /* dd($language, $version, $remember); */
        $report = $this->versionManager->validateSelection($version, $language);

        $backUrl = $request->headers->get('referer') ?: $this->generateUrl('app_setup');

        // (optionnel) sécurité basique: ne redirige que vers le même host
        if (!str_starts_with($backUrl, $request->getSchemeAndHttpHost())) {
            $backUrl = $this->generateUrl('app_setup');
        }

        if (!$report['ok']) {
            foreach ($report['errors'] as $field => $msg) {
                $this->addFlash('error', sprintf('%s: %s', ucfirst($field), $msg));
            }
            return $this->redirect($backUrl);
        }

        // Succès
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
        $this->addFlash('success', 'Préférences reçues 👍');
        return $response;
    }
}
