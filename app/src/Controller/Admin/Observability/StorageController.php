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
 * Detailed object-storage analytics (MinIO). The report — a deep bucket listing —
 * is a deferred panel, so the page never waits on it. ROLE_ADMIN via the /admin
 * firewall.
 */
#[Route('/admin')]
final class StorageController extends AbstractController
{
    public function __construct(private readonly AdminPanelCatalog $panels) {}

    #[Route('/storage', name: 'admin_storage', methods: ['GET'])]
    public function storage(Request $request): Response
    {
        return $this->render(
            'admin/storage.html.twig',
            $this->panels->pageContext(PanelContext::fromRequest($request), ['storage']),
        );
    }
}
