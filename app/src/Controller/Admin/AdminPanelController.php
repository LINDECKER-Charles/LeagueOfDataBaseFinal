<?php
declare(strict_types=1);

namespace App\Controller\Admin;

use App\Service\Admin\AdminPanelCatalog;
use App\Service\Admin\PanelContext;
use Symfony\Bundle\FrameworkBundle\Controller\AbstractController;
use Symfony\Component\HttpFoundation\Request;
use Symfony\Component\HttpFoundation\Response;
use Symfony\Component\Routing\Attribute\Route;

/**
 * HTML fragments for the dashboards' deferred panels, fetched by
 * public/admin/js/panels.js once the page shell is painted. ROLE_ADMIN via the
 * /admin firewall, like every route below /admin.
 */
#[Route('/admin')]
final class AdminPanelController extends AbstractController
{
    public function __construct(private readonly AdminPanelCatalog $panels) {}

    #[Route(
        '/panel/{panel}',
        name: 'admin_panel',
        requirements: ['panel' => '[a-z-]+'],
        methods: ['GET']
    )]
    public function panel(string $panel, Request $request): Response
    {
        if (!$this->panels->has($panel)) {
            throw $this->createNotFoundException(sprintf('Unknown admin panel "%s".', $panel));
        }
        $this->releaseSessionLock($request);

        // Reports are already memoised server-side; a stored fragment would only
        // let a stale panel survive a "Rafraîchir".
        return new Response(
            $this->panels->render($panel, PanelContext::fromRequest($request)),
            headers: ['Cache-Control' => 'no-store'],
        );
    }

    /**
     * A page ships one request per panel *on purpose*, so they must actually
     * resolve in parallel. PHP's native file session handler holds an exclusive
     * lock for the whole request, which queued the fragments one behind the other
     * — the very serialisation deferring them was meant to remove. Authentication
     * is already resolved here and a fragment writes nothing, so the session is
     * closed before the (slow) render; the firewall reopens it just long enough
     * to store the token back at kernel.response.
     */
    private function releaseSessionLock(Request $request): void
    {
        if ($request->hasSession() && $request->getSession()->isStarted()) {
            $request->getSession()->save();
        }
    }
}
