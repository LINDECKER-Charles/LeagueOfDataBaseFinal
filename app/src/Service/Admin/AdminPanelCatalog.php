<?php
declare(strict_types=1);

namespace App\Service\Admin;

use App\Service\Analytics\AnalyticsReportService;
use App\Service\Analytics\GeoLocator;
use App\Service\Analytics\StorageAnalyticsService;
use Twig\Environment;

/**
 * Single source of truth for the admin's deferred panels: which template renders
 * a panel and which report it needs.
 *
 * Admin dashboards aggregate reports that each cost a cold round trip (S3 deep
 * listing, NDJSON scan, service probes), so the pages ship a shell plus one
 * skeleton per panel and the fragments resolve independently — see
 * {@see \App\Controller\Admin\AdminPanelController} and public/admin/js/panels.js.
 * The `?sync=1` fallback (no-JavaScript clients, reached through a `<noscript>`
 * refresh) renders the very same fragments inline through {@see pageContext},
 * so the two paths cannot drift.
 */
final class AdminPanelCatalog
{
    /** Panels the overview page defers, in render order. */
    public const OVERVIEW_PANELS = ['overview-app', 'overview-traffic', 'overview-storage'];

    private const TEMPLATES = [
        'overview-app' => 'admin/panel/_overview_app.html.twig',
        'overview-traffic' => 'admin/panel/_overview_traffic.html.twig',
        'overview-storage' => 'admin/panel/_overview_storage.html.twig',
        'traffic' => 'admin/panel/_traffic.html.twig',
        'audience' => 'admin/panel/_audience.html.twig',
        'storage' => 'admin/panel/_storage.html.twig',
        'monitoring' => 'admin/panel/_monitoring.html.twig',
    ];

    public function __construct(
        private readonly Environment $twig,
        private readonly AnalyticsReportService $analytics,
        private readonly StorageAnalyticsService $storage,
        private readonly MonitoringReportService $monitoring,
        private readonly GeoLocator $geo,
    ) {}

    public function has(string $panel): bool
    {
        return isset(self::TEMPLATES[$panel]);
    }

    public function render(string $panel, PanelContext $context): string
    {
        if (!$this->has($panel)) {
            throw new \InvalidArgumentException(sprintf('Unknown admin panel "%s".', $panel));
        }

        return $this->twig->render(self::TEMPLATES[$panel], $this->data($panel, $context));
    }

    /**
     * Template variables for a page hosting `$panels`: the panels are pre-rendered
     * only in synchronous mode, otherwise the page emits skeletons and the client
     * fetches each fragment.
     *
     * @param list<string> $panels
     * @return array{sync: bool, refresh: bool, panels: array<string, string>}
     */
    public function pageContext(PanelContext $context, array $panels): array
    {
        $rendered = [];
        foreach ($context->sync ? $panels : [] as $panel) {
            $rendered[$panel] = $this->render($panel, $context);
        }

        return ['sync' => $context->sync, 'refresh' => $context->fresh, 'panels' => $rendered];
    }

    /** @return array<string, mixed> */
    private function data(string $panel, PanelContext $context): array
    {
        return match ($panel) {
            'overview-app', 'monitoring' => ['report' => $this->monitoring->report($context->fresh)],
            'overview-traffic', 'traffic' => $this->analyticsData($context),
            'audience' => $this->analyticsData($context) + ['geo_available' => $this->geo->isAvailable()],
            'overview-storage', 'storage' => ['report' => $this->storage->report($context->fresh)],
        };
    }

    /** @return array{report: array<string, mixed>, range: string} */
    private function analyticsData(PanelContext $context): array
    {
        $range = $this->analytics->normalizeRange($context->range);

        return ['report' => $this->analytics->report($range, $context->fresh), 'range' => $range];
    }
}
