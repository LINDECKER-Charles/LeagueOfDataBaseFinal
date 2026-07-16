<?php
declare(strict_types=1);

namespace App\Security;

use Symfony\Component\DependencyInjection\Attribute\Autowire;
use Symfony\Component\Security\Core\Exception\UnsupportedUserException;
use Symfony\Component\Security\Core\Exception\UserNotFoundException;
use Symfony\Component\Security\Core\User\InMemoryUser;
use Symfony\Component\Security\Core\User\UserInterface;
use Symfony\Component\Security\Core\User\UserProviderInterface;

/**
 * Backs the `admin` firewall with the single env-defined admin identity.
 *
 * A real provider (rather than an empty in-memory one) is required so the firewall
 * can refresh the token on every subsequent request; otherwise the session token is
 * discarded after the login request and the admin is silently logged out.
 *
 * @implements UserProviderInterface<InMemoryUser>
 */
final class AdminUserProvider implements UserProviderInterface
{
    public function __construct(
        #[Autowire(env: 'ADMIN_LOGIN')] #[\SensitiveParameter] private readonly string $adminLogin,
    ) {}

    public function loadUserByIdentifier(string $identifier): UserInterface
    {
        if (!hash_equals($this->adminLogin, $identifier)) {
            throw new UserNotFoundException();
        }

        return new InMemoryUser($this->adminLogin, null, ['ROLE_ADMIN']);
    }

    public function refreshUser(UserInterface $user): UserInterface
    {
        if (!$user instanceof InMemoryUser) {
            throw new UnsupportedUserException(sprintf('Unsupported user class "%s".', $user::class));
        }

        return $this->loadUserByIdentifier($user->getUserIdentifier());
    }

    public function supportsClass(string $class): bool
    {
        return InMemoryUser::class === $class || is_subclass_of($class, InMemoryUser::class);
    }
}
