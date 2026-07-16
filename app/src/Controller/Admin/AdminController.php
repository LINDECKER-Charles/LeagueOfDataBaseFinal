<?php
declare(strict_types=1);

namespace App\Controller\Admin;

use App\Service\Storage\StorageUsageService;
use Symfony\Bundle\FrameworkBundle\Controller\AbstractController;
use Symfony\Component\HttpFoundation\Response;
use Symfony\Component\Routing\Attribute\Route;
use Symfony\Component\Security\Http\Authentication\AuthenticationUtils;

/**
 * Admin panel. Everything under /admin is gated by ROLE_ADMIN through the `admin`
 * firewall (see config/packages/security.yaml); only the login page is public.
 */
#[Route('/admin')]
final class AdminController extends AbstractController
{
    #[Route('/login', name: 'admin_login', methods: ['GET', 'POST'])]
    public function login(AuthenticationUtils $authenticationUtils): Response
    {
        if ($this->getUser() !== null) {
            return $this->redirectToRoute('admin_storage');
        }

        return $this->render('admin/login.html.twig', [
            'last_username' => $authenticationUtils->getLastUsername(),
            'error' => $authenticationUtils->getLastAuthenticationError(),
        ]);
    }

    #[Route('/logout', name: 'admin_logout', methods: ['GET'])]
    public function logout(): never
    {
        throw new \LogicException('Intercepted by the logout key on the admin firewall.');
    }

    #[Route('', name: 'admin_dashboard', methods: ['GET'])]
    public function dashboard(): Response
    {
        return $this->redirectToRoute('admin_storage');
    }

    #[Route('/storage', name: 'admin_storage', methods: ['GET'])]
    public function storage(StorageUsageService $storageUsage): Response
    {
        return $this->render('admin/storage.html.twig', [
            'report' => $storageUsage->report(),
        ]);
    }
}
