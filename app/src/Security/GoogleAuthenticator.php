<?php
declare(strict_types=1);

namespace App\Security;

use App\Entity\User;
use KnpU\OAuth2ClientBundle\Client\ClientRegistry;
use KnpU\OAuth2ClientBundle\Security\Authenticator\OAuth2Authenticator;
use League\OAuth2\Client\Provider\GoogleUser;
use Symfony\Component\DependencyInjection\Attribute\Autowire;
use Symfony\Component\HttpFoundation\RedirectResponse;
use Symfony\Component\HttpFoundation\Request;
use Symfony\Component\HttpFoundation\Response;
use Symfony\Component\HttpFoundation\Session\FlashBagAwareSessionInterface;
use Symfony\Component\Routing\Generator\UrlGeneratorInterface;
use Symfony\Component\Security\Core\Authentication\Token\TokenInterface;
use Symfony\Component\Security\Core\Exception\AuthenticationException;
use Symfony\Component\Security\Http\Authenticator\Passport\Badge\UserBadge;
use Symfony\Component\Security\Http\Authenticator\Passport\Passport;
use Symfony\Component\Security\Http\Authenticator\Passport\SelfValidatingPassport;
use Symfony\Contracts\Translation\TranslatorInterface;

/**
 * Handles the Google OAuth callback (/connect/google/check) on the main
 * firewall. The passport is self-validating: Google already authenticated the
 * user; our only job is to resolve/provision the matching site account.
 */
final class GoogleAuthenticator extends OAuth2Authenticator
{
    private const CLIENT = 'google';
    private const CHECK_ROUTE = 'connect_google_check';

    public function __construct(
        private readonly ClientRegistry $clientRegistry,
        private readonly GoogleAccountProvisioner $provisioner,
        private readonly UrlGeneratorInterface $urlGenerator,
        private readonly TranslatorInterface $translator,
        #[Autowire(env: 'OAUTH_GOOGLE_CLIENT_ID')]
        private readonly string $googleClientId,
    ) {
    }

    public function supports(Request $request): ?bool
    {
        // Unconfigured OAuth: decline, so the controller degrades to the login page.
        return $this->googleClientId !== ''
            && $request->attributes->get('_route') === self::CHECK_ROUTE;
    }

    public function authenticate(Request $request): Passport
    {
        $client = $this->clientRegistry->getClient(self::CLIENT);
        $accessToken = $this->fetchAccessToken($client);

        return new SelfValidatingPassport(new UserBadge(
            $accessToken->getToken(),
            function () use ($accessToken, $client): User {
                $googleUser = $client->fetchUserFromToken($accessToken);
                \assert($googleUser instanceof GoogleUser);

                return $this->provisioner->provision($googleUser);
            },
        ));
    }

    public function onAuthenticationSuccess(Request $request, TokenInterface $token, string $firewallName): ?Response
    {
        return new RedirectResponse($this->urlGenerator->generate('app_profile'));
    }

    public function onAuthenticationFailure(Request $request, AuthenticationException $exception): ?Response
    {
        $session = $request->hasSession() ? $request->getSession() : null;
        if ($session instanceof FlashBagAwareSessionInterface) {
            $session->getFlashBag()->add('error', $this->translator->trans('auth.google.failed'));
        }

        return new RedirectResponse($this->urlGenerator->generate('app_login'));
    }
}
