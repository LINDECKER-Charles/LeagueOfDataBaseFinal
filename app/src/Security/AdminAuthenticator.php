<?php
declare(strict_types=1);

namespace App\Security;

use Symfony\Component\DependencyInjection\Attribute\Autowire;
use Symfony\Component\HttpFoundation\RedirectResponse;
use Symfony\Component\HttpFoundation\Request;
use Symfony\Component\HttpFoundation\Response;
use Symfony\Component\Routing\Generator\UrlGeneratorInterface;
use Symfony\Component\Security\Core\Authentication\Token\TokenInterface;
use Symfony\Component\Security\Core\Exception\CustomUserMessageAuthenticationException;
use Symfony\Component\Security\Http\Authenticator\AbstractLoginFormAuthenticator;
use Symfony\Component\Security\Http\Authenticator\Passport\Badge\CsrfTokenBadge;
use Symfony\Component\Security\Http\Authenticator\Passport\Badge\UserBadge;
use Symfony\Component\Security\Http\Authenticator\Passport\Passport;
use Symfony\Component\Security\Http\Authenticator\Passport\SelfValidatingPassport;
use Symfony\Component\Security\Http\SecurityRequestAttributes;
use Symfony\Component\Security\Http\Util\TargetPathTrait;

/**
 * Single-admin form-login authenticator.
 *
 * There is no user database: credentials live in ADMIN_LOGIN / ADMIN_PASSWORD
 * (plaintext env, injected via compose). A {@see SelfValidatingPassport} is used
 * because the password is verified here in constant time rather than by a hasher,
 * which lets the env value stay plaintext without registering an insecure
 * `plaintext` password hasher on the whole app.
 */
final class AdminAuthenticator extends AbstractLoginFormAuthenticator
{
    use TargetPathTrait;

    public const LOGIN_ROUTE = 'admin_login';
    private const CSRF_TOKEN_ID = 'authenticate';

    public function __construct(
        private readonly UrlGeneratorInterface $urlGenerator,
        #[Autowire(env: 'ADMIN_LOGIN')] #[\SensitiveParameter] private readonly string $adminLogin,
        #[Autowire(env: 'ADMIN_PASSWORD')] #[\SensitiveParameter] private readonly string $adminPassword,
    ) {}

    public function authenticate(Request $request): Passport
    {
        $username = trim((string) $request->request->get('username', ''));
        $password = (string) $request->request->get('password', '');

        $request->getSession()->set(SecurityRequestAttributes::LAST_USERNAME, $username);

        // Compute both comparisons unconditionally to avoid leaking, via timing,
        // whether the username alone matched.
        $loginMatches = hash_equals($this->adminLogin, $username);
        $passwordMatches = hash_equals($this->adminPassword, $password);

        if (!($loginMatches && $passwordMatches)) {
            throw new CustomUserMessageAuthenticationException('Identifiants invalides.');
        }

        // The user is resolved through App\Security\AdminUserProvider (the firewall's
        // provider), keeping a single source of truth for the admin identity.
        return new SelfValidatingPassport(
            new UserBadge($this->adminLogin),
            [new CsrfTokenBadge(self::CSRF_TOKEN_ID, (string) $request->request->get('_csrf_token', ''))],
        );
    }

    public function onAuthenticationSuccess(Request $request, TokenInterface $token, string $firewallName): ?Response
    {
        $target = $this->getTargetPath($request->getSession(), $firewallName)
            ?? $this->urlGenerator->generate('admin_storage');

        return new RedirectResponse($target);
    }

    protected function getLoginUrl(Request $request): string
    {
        return $this->urlGenerator->generate(self::LOGIN_ROUTE);
    }
}
