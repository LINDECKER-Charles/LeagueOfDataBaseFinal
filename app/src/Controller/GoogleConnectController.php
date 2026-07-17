<?php
declare(strict_types=1);

namespace App\Controller;

use KnpU\OAuth2ClientBundle\Client\ClientRegistry;
use Symfony\Bundle\FrameworkBundle\Controller\AbstractController;
use Symfony\Component\DependencyInjection\Attribute\Autowire;
use Symfony\Component\HttpFoundation\RedirectResponse;
use Symfony\Component\HttpFoundation\Response;
use Symfony\Component\Routing\Attribute\Route;
use Symfony\Contracts\Translation\TranslatorInterface;

/**
 * Entry points of "Sign in with Google". /connect/google sends the browser to
 * Google's consent screen; /connect/google/check is normally intercepted by
 * GoogleAuthenticator — its action only runs when the authenticator declined
 * (OAuth not configured, stray visit) and degrades cleanly to the login page.
 */
final class GoogleConnectController extends AbstractController
{
    private const SCOPES = ['openid', 'profile', 'email'];

    public function __construct(
        private readonly TranslatorInterface $translator,
        #[Autowire(env: 'OAUTH_GOOGLE_CLIENT_ID')]
        private readonly string $googleClientId,
    ) {
    }

    #[Route('/connect/google', name: 'connect_google_start', methods: ['GET'])]
    public function start(ClientRegistry $clientRegistry): Response
    {
        if ($this->googleClientId === '') {
            return $this->redirectToLoginUnavailable();
        }

        return $clientRegistry->getClient('google')->redirect(self::SCOPES, []);
    }

    #[Route('/connect/google/check', name: 'connect_google_check', methods: ['GET'])]
    public function check(): RedirectResponse
    {
        return $this->redirectToLoginUnavailable();
    }

    private function redirectToLoginUnavailable(): RedirectResponse
    {
        $this->addFlash('error', $this->translator->trans('auth.google.unavailable'));

        return $this->redirectToRoute('app_login');
    }
}
