<?php
declare(strict_types=1);

namespace App\Controller;

use App\Entity\User;
use Symfony\Component\HttpFoundation\Response;
use Symfony\Component\Routing\Attribute\Route;
use Symfony\Component\Security\Http\Authentication\AuthenticationUtils;

/**
 * Public-site login/logout (main firewall). Extends the resource base so the
 * page provides the `client` view-model base.html.twig relies on (dd-version
 * meta, persistent islands, bottom nav).
 */
final class SecurityController extends AbstractResourceController
{
    #[Route('/login', name: 'app_login', methods: ['GET', 'POST'])]
    public function login(AuthenticationUtils $authenticationUtils): Response
    {
        if ($this->getUser() instanceof User) {
            return $this->redirectToRoute('app_profile');
        }

        return $this->render('security/login.html.twig', [
            'client' => $this->clientData(),
            'last_username' => $authenticationUtils->getLastUsername(),
            'error' => $authenticationUtils->getLastAuthenticationError(),
        ]);
    }

    #[Route('/logout', name: 'app_logout', methods: ['POST'])]
    public function logout(): never
    {
        // Intercepted by the firewall's logout listener; never executed.
        throw new \LogicException('Handled by the logout key on the main firewall.');
    }
}
