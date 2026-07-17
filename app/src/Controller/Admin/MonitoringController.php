<?php
declare(strict_types=1);

namespace App\Controller\Admin;

use App\Service\Admin\MonitoringReportService;
use Symfony\Bundle\FrameworkBundle\Controller\AbstractController;
use Symfony\Component\HttpFoundation\Request;
use Symfony\Component\HttpFoundation\Response;
use Symfony\Component\Routing\Attribute\Route;

/**
 * Service health + application counters + storage volumes, memoised 30 s
 * (`?refresh=1` forces a new probe round). ROLE_ADMIN via the /admin firewall.
 */
#[Route('/admin')]
final class MonitoringController extends AbstractController
{
    #[Route('/monitoring', name: 'admin_monitoring', methods: ['GET'])]
    public function index(Request $request, MonitoringReportService $monitoring): Response
    {
        return $this->render('admin/monitoring.html.twig', [
            'report' => $monitoring->report(fresh: $request->query->getBoolean('refresh')),
        ]);
    }
}
