<?php
declare(strict_types=1);

namespace App\Controller\Admin;

use App\Service\Admin\AdminPanelCatalog;
use App\Service\Admin\PanelContext;
use App\Service\Analytics\AnalyticsReportService;
use Symfony\Bundle\FrameworkBundle\Controller\AbstractController;
use Symfony\Component\HttpFoundation\Request;
use Symfony\Component\HttpFoundation\Response;
use Symfony\Component\Routing\Attribute\Route;
use Symfony\Component\Security\Http\Authentication\AuthenticationUtils;

/**
 * Admin entry points: authentication + the cross-subsystem overview. Everything
 * under /admin is gated by ROLE_ADMIN through the `admin` firewall (see
 * config/packages/security.yaml); only the login page is public.
 */
#[Route('/admin')]
final class AdminController extends AbstractController
{
    #[Route('/login', name: 'admin_login', methods: ['GET', 'POST'])]
    public function login(AuthenticationUtils $authenticationUtils): Response
    {
        if ($this->getUser() !== null) {
            return $this->redirectToRoute('admin_dashboard');
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

    /**
     * The overview aggregates three independent reports; each ships as a deferred
     * panel, so the page paints before the slowest one resolves.
     */
    #[Route('', name: 'admin_dashboard', methods: ['GET'])]
    public function dashboard(
        Request $request,
        AnalyticsReportService $analytics,
        AdminPanelCatalog $panels
    ): Response {
        $context = PanelContext::fromRequest($request);

        return $this->render('admin/overview.html.twig', [
            'range' => $analytics->normalizeRange($context->range),
            'ranges' => $analytics->ranges(),
        ] + $panels->pageContext($context, AdminPanelCatalog::OVERVIEW_PANELS));
    }
}
