<?php
declare(strict_types=1);

namespace App\Controller\Admin\Observability;

use App\Service\Admin\AdminPanelCatalog;
use App\Service\Admin\PanelContext;
use Symfony\Bundle\FrameworkBundle\Controller\AbstractController;
use Symfony\Component\HttpFoundation\Request;
use Symfony\Component\HttpFoundation\Response;
use Symfony\Component\Routing\Attribute\Route;

/**
 * Service health + application counters + storage volumes, memoised 30 s
 * (`?refresh=1` forces a new probe round). The probes are live round trips, so
 * the report ships as a deferred panel. ROLE_ADMIN via the /admin firewall.
 */
#[Route('/admin')]
final class MonitoringController extends AbstractController
{
    public function __construct(private readonly AdminPanelCatalog $panels) {}

    #[Route('/monitoring', name: 'admin_monitoring', methods: ['GET'])]
    public function index(Request $request): Response
    {
        return $this->render(
            'admin/monitoring.html.twig',
            $this->panels->pageContext(PanelContext::fromRequest($request), ['monitoring']),
        );
    }
}
