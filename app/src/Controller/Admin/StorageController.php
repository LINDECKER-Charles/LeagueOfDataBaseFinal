<?php
declare(strict_types=1);

namespace App\Controller\Admin;

use App\Service\Analytics\StorageAnalyticsService;
use Symfony\Bundle\FrameworkBundle\Controller\AbstractController;
use Symfony\Component\HttpFoundation\Request;
use Symfony\Component\HttpFoundation\Response;
use Symfony\Component\Routing\Attribute\Route;

/**
 * Detailed object-storage analytics (MinIO). ROLE_ADMIN via the /admin firewall.
 */
#[Route('/admin')]
final class StorageController extends AbstractController
{
    #[Route('/storage', name: 'admin_storage', methods: ['GET'])]
    public function storage(Request $request, StorageAnalyticsService $storage): Response
    {
        return $this->render('admin/storage.html.twig', [
            'report' => $storage->report(fresh: $request->query->getBoolean('refresh')),
        ]);
    }
}
