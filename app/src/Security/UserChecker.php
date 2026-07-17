<?php
declare(strict_types=1);

namespace App\Security;

use App\Entity\User;
use Symfony\Component\Security\Core\Exception\CustomUserMessageAccountStatusException;
use Symfony\Component\Security\Core\User\UserCheckerInterface;
use Symfony\Component\Security\Core\User\UserInterface;
use Symfony\Contracts\Translation\TranslatorInterface;

/**
 * Blocks banned accounts on the `main` firewall — every authenticator (form
 * login, Google, remember-me) funnels through checkPreAuth. The message is
 * translated HERE (current request locale): the login template renders the
 * exception's messageKey through the `security` domain, where our custom
 * sentence has no entry and therefore passes through verbatim.
 */
final class UserChecker implements UserCheckerInterface
{
    private const MESSAGE_KEY = 'auth.flash.banned';

    public function __construct(private readonly TranslatorInterface $translator) {}

    public function checkPreAuth(UserInterface $user): void
    {
        if (!$user instanceof User || !$user->isBanned()) {
            return;
        }

        throw new CustomUserMessageAccountStatusException($this->translator->trans(self::MESSAGE_KEY));
    }

    public function checkPostAuth(UserInterface $user): void
    {
        // Ban state is fully known pre-auth; nothing to verify afterwards.
    }
}
