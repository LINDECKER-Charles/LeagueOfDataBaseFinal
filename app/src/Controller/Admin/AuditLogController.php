<?php
declare(strict_types=1);

namespace App\Controller\Admin;

use App\Entity\User;
use App\Service\Audit\AuditAction;
use App\Service\Audit\AuditCategory;
use App\Service\Audit\AuditFilter;
use App\Service\Audit\AuditLogger;
use App\Service\Audit\AuditQueryService;
use App\Service\Audit\AuditRollupService;
use Symfony\Component\HttpFoundation\Request;
use Symfony\Component\HttpFoundation\Response;
use Symfony\Component\Routing\Attribute\Route;

/**
 * Audit journal: the global activity feed, the per-account activity view
 * ("every action of user X"), and the operator-triggered, period-scoped purge.
 * ROLE_ADMIN via the /admin firewall; the purge is a CSRF-checked POST and is
 * itself audited ({@see AuditAction::AdminLogsPurge}).
 */
#[Route('/admin')]
final class AuditLogController extends AbstractAdminController
{
    private const PER_PAGE_LOGS = 40;
    private const DATE_RE = '/^\d{4}-\d{2}-\d{2}$/';

    public function __construct(
        private readonly AuditQueryService $query,
        private readonly AuditRollupService $rollup,
        private readonly AuditLogger $audit,
    ) {}

    #[Route('/logs', name: 'admin_logs', methods: ['GET'])]
    public function index(Request $request): Response
    {
        $page = $this->pageParam($request);
        ['rows' => $rows, 'hasMore' => $hasMore] = $this->query->recent($this->filter($request), $page, self::PER_PAGE_LOGS);

        return $this->render('admin/logs.html.twig', [
            'rows' => $rows,
            'page' => $page,
            'hasMore' => $hasMore,
            'filters' => $this->filterContext($request),
            'categories' => AuditCategory::cases(),
            'volume' => $this->query->volume(),
            'retentionCutoff' => $this->rollup->retentionCutoff(),
        ]);
    }

    #[Route('/users/{id}/activity', name: 'admin_user_activity', methods: ['GET'])]
    public function userActivity(User $user, Request $request): Response
    {
        $page = $this->pageParam($request);
        ['rows' => $rows, 'hasMore' => $hasMore] = $this->query->forUser($user, $this->filter($request), $page, self::PER_PAGE_LOGS);

        return $this->render('admin/user_activity.html.twig', [
            'subject' => $user,
            'rows' => $rows,
            'page' => $page,
            'hasMore' => $hasMore,
            'filters' => $this->filterContext($request),
            'categories' => AuditCategory::cases(),
        ]);
    }

    #[Route('/logs/purge', name: 'admin_logs_purge', methods: ['POST'])]
    public function purge(Request $request): Response
    {
        if (!$this->isCsrfTokenValid('admin_logs_purge', (string) $request->request->get('_token'))) {
            $this->addFlash('error', 'Jeton CSRF invalide.');

            return $this->redirectToRoute('admin_logs');
        }

        $scope = (string) $request->request->get('scope', '');
        [$before, $all] = $this->purgeBounds($scope, $request);
        if (!$all && $before === null) {
            $this->addFlash('error', 'Période de purge invalide.');

            return $this->redirectToRoute('admin_logs');
        }

        $result = $this->rollup->purge($before, $all);
        $this->audit->log(AuditAction::AdminLogsPurge, metadata: [
            'scope' => $scope,
            'days' => count($result['deleted']),
            'bytes' => $result['freedBytes'],
        ]);
        $this->addFlash('success', sprintf(
            'Purge effectuée : %d journée(s) supprimée(s), ~%s libéré(s).',
            count($result['deleted']),
            $this->humanBytes($result['freedBytes']),
        ));

        return $this->redirectToRoute('admin_logs');
    }

    private function filter(Request $request): AuditFilter
    {
        return new AuditFilter(
            AuditCategory::tryFrom((string) $request->query->get('category', '')),
            $this->parseDate((string) $request->query->get('from', ''), endOfDay: false),
            $this->parseDate((string) $request->query->get('to', ''), endOfDay: true),
        );
    }

    /** @return array{category: string, from: string, to: string} */
    private function filterContext(Request $request): array
    {
        return [
            'category' => (string) $request->query->get('category', ''),
            'from' => (string) $request->query->get('from', ''),
            'to' => (string) $request->query->get('to', ''),
        ];
    }

    /** @return array{0: ?\DateTimeImmutable, 1: bool} */
    private function purgeBounds(string $scope, Request $request): array
    {
        return match ($scope) {
            'all' => [null, true],
            'retention' => [$this->rollup->retentionCutoff(), false],
            'before' => [$this->parseDate((string) $request->request->get('before', ''), endOfDay: false), false],
            default => [null, false],
        };
    }

    private function parseDate(string $value, bool $endOfDay): ?\DateTimeImmutable
    {
        if (!preg_match(self::DATE_RE, $value)) {
            return null;
        }

        try {
            return new \DateTimeImmutable($value . ($endOfDay ? ' 23:59:59' : ' 00:00:00'), new \DateTimeZone('UTC'));
        } catch (\Throwable) {
            return null;
        }
    }

    private function humanBytes(int $bytes): string
    {
        if ($bytes < 1024) {
            return $bytes . ' o';
        }
        $units = ['Kio', 'Mio', 'Gio'];
        $value = $bytes / 1024;
        $unit = 0;
        while ($value >= 1024 && $unit < count($units) - 1) {
            $value /= 1024;
            ++$unit;
        }

        return number_format($value, 1, ',', ' ') . ' ' . $units[$unit];
    }
}
