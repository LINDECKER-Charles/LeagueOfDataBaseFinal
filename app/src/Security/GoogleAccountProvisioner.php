<?php
declare(strict_types=1);

namespace App\Security;

use App\Entity\User;
use App\Repository\UserRepository;
use Doctrine\ORM\EntityManagerInterface;
use League\OAuth2\Client\Provider\GoogleUser;
use Symfony\Component\Security\Core\Exception\CustomUserMessageAuthenticationException;

/**
 * Maps an authenticated Google identity to a site account:
 *  1. by googleId (stable OpenID `sub` claim);
 *  2. else attaches to the existing account with the same email — only when
 *     Google asserts `email_verified`, otherwise an attacker could register an
 *     unverified address at Google and take over the matching site account;
 *  3. else creates a fresh account (username derived from the profile, no
 *     password until the user sets one on /profile).
 */
final class GoogleAccountProvisioner
{
    public function __construct(
        private readonly UserRepository $users,
        private readonly EntityManagerInterface $entityManager,
        private readonly UsernameAllocator $usernameAllocator,
    ) {
    }

    public function provision(GoogleUser $googleUser): User
    {
        $googleId = (string) $googleUser->getId();
        if ($googleId === '') {
            throw new CustomUserMessageAuthenticationException('Google profile has no subject id.');
        }

        $known = $this->users->findOneBy(['googleId' => $googleId]);
        if ($known !== null) {
            return $known;
        }

        $user = $this->attachByVerifiedEmail($googleUser, $googleId) ?? $this->createAccount($googleUser, $googleId);
        // Both paths require a Google-verified email, so the address is proven:
        // skip our own confirmation step for accounts arriving through Google.
        $user->setIsVerified(true);
        $this->entityManager->flush();

        return $user;
    }

    private function attachByVerifiedEmail(GoogleUser $googleUser, string $googleId): ?User
    {
        if (($googleUser->toArray()['email_verified'] ?? false) !== true) {
            return null;
        }

        $user = $this->users->findOneBy(['email' => mb_strtolower($this->email($googleUser))]);

        return $user?->setGoogleId($googleId);
    }

    private function createAccount(GoogleUser $googleUser, string $googleId): User
    {
        $email = $this->email($googleUser);
        $localPart = explode('@', $email, 2)[0];
        $username = $this->usernameAllocator->allocate(
            [(string) $googleUser->getFirstName(), $localPart],
            fn (string $candidate): bool => $this->users->findOneByUsernameInsensitive($candidate) !== null,
        );

        $user = (new User())
            ->setEmail($email)
            ->setUsername($username)
            ->setGoogleId($googleId);
        $this->entityManager->persist($user);

        return $user;
    }

    private function email(GoogleUser $googleUser): string
    {
        $email = (string) $googleUser->getEmail();
        if ($email === '') {
            // The `email` scope is always requested; an empty claim means a broken flow.
            throw new CustomUserMessageAuthenticationException('Google profile exposes no email.');
        }

        return $email;
    }
}
