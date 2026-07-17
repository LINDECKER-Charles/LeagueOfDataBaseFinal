<?php
declare(strict_types=1);

namespace App\Controller\Admin;

use App\Service\Analytics\AnalyticsReportService;
use App\Service\Analytics\GeoLocator;
use App\Service\Analytics\RollupService;
use App\Service\Audit\AuditAction;
use App\Service\Audit\AuditLogger;
use Symfony\Bundle\FrameworkBundle\Controller\AbstractController;
use Symfony\Component\HttpFoundation\Request;
use Symfony\Component\HttpFoundation\Response;
use Symfony\Component\Routing\Attribute\Route;

/**
 * Traffic (most-consulted pages) and audience (who visits) dashboards, plus the
 * manual rollup trigger. ROLE_ADMIN via the /admin firewall.
 */
#[Route('/admin')]
final class AnalyticsController extends AbstractController
{
    public function __construct(private readonly AnalyticsReportService $analytics) {}

    #[Route('/traffic', name: 'admin_traffic', methods: ['GET'])]
    public function traffic(Request $request): Response
    {
        return $this->render('admin/traffic.html.twig', $this->context($request));
    }

    #[Route('/audience', name: 'admin_audience', methods: ['GET'])]
    public function audience(Request $request, GeoLocator $geo): Response
    {
        return $this->render('admin/audience.html.twig', $this->context($request) + [
            'geo_available' => $geo->isAvailable(),
        ]);
    }

    #[Route('/analytics/rollup', name: 'admin_analytics_rollup', methods: ['POST'])]
    public function rollup(Request $request, RollupService $rollup, AuditLogger $audit): Response
    {
        if (!$this->isCsrfTokenValid('analytics_rollup', (string) $request->request->get('_token'))) {
            $this->addFlash('error', 'Jeton CSRF invalide.');

            return $this->redirectToRoute('admin_dashboard');
        }

        $result = $rollup->rollup(includeToday: true);
        $audit->log(AuditAction::AdminAnalyticsRollup, metadata: ['rolled' => count($result['rolled'])]);
        $this->addFlash('success', sprintf(
            'Consolidation terminée : %d journée(s) écrite(s) dans MinIO.',
            count($result['rolled']),
        ));

        return $this->redirectToRoute('admin_dashboard');
    }

    /**
     * @return array{report: array<string, mixed>, range: string, ranges: list<string>}
     */
    private function context(Request $request): array
    {
        $range = $this->analytics->normalizeRange((string) $request->query->get('range', '30d'));

        return [
            'report' => $this->analytics->report($range),
            'range' => $range,
            'ranges' => $this->analytics->ranges(),
        ];
    }
}
