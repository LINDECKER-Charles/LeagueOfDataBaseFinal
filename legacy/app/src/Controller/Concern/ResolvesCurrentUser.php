<?php
declare(strict_types=1);

namespace App\Controller\Concern;

use App\Entity\User;

/**
 * Resolves the authenticated {@see User} for controllers behind a ROLE_USER
 * firewall. The `instanceof` guard is not redundant with access_control: it
 * pins the concrete identity (guarding against an admin-firewall token or a
 * misconfigured access rule) and narrows the type for static analysis.
 *
 * For controllers extending {@see \Symfony\Bundle\FrameworkBundle\Controller\AbstractController}.
 */
trait ResolvesCurrentUser
{
    private function currentUser(): User
    {
        $user = $this->getUser();
        if (!$user instanceof User) {
            throw $this->createAccessDeniedException();
        }

        return $user;
    }
}
