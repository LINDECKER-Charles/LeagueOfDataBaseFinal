<?php
declare(strict_types=1);

namespace App\Controller\Admin;

use App\Entity\Build;
use App\Repository\BuildRepository;
use App\Repository\BuildVoteRepository;
use Doctrine\ORM\EntityManagerInterface;
use Symfony\Component\HttpFoundation\Request;
use Symfony\Component\HttpFoundation\Response;
use Symfony\Component\Routing\Attribute\Route;

/**
 * Build moderation: search (name/champion), visibility filter, unpublish and
 * delete. ROLE_ADMIN via the /admin firewall; mutations are CSRF-checked POSTs.
 */
#[Route('/admin')]
final class BuildModerationController extends AbstractAdminController
{
    private const VISIBILITIES = ['public', 'private'];

    public function __construct(
        private readonly BuildRepository $builds,
        private readonly BuildVoteRepository $votes,
        private readonly EntityManagerInterface $entityManager,
    ) {}

    #[Route('/builds', name: 'admin_builds', methods: ['GET'])]
    public function index(Request $request): Response
    {
        $query = trim((string) $request->query->get('q', ''));
        $visibility = (string) $request->query->get('visibility', '');
        $visibility = \in_array($visibility, self::VISIBILITIES, true) ? $visibility : '';
        $page = $this->pageParam($request);

        ['builds' => $builds, 'total' => $total] = $this->builds
            ->searchPaginated($query, $visibility === '' ? null : $visibility === 'public', $page, self::PER_PAGE);

        return $this->render('admin/builds.html.twig', [
            'rows' => $this->rows($builds),
            'total' => $total,
            'q' => $query,
            'visibility' => $visibility,
            'page' => $page,
            'pages' => $this->pageCount($total),
            'stats' => ['total' => $this->builds->countAll(), 'public' => $this->builds->countPublic()],
        ]);
    }

    #[Route('/builds/{id}/unpublish', name: 'admin_build_unpublish', methods: ['POST'])]
    public function unpublish(Build $build, Request $request): Response
    {
        if ($error = $this->csrfError($request, 'admin_build_unpublish', 'admin_builds')) {
            return $error;
        }

        $build->setIsPublic(false);
        $this->entityManager->flush();
        $this->addFlash('success', sprintf('Build « %s » dépublié — il reste visible par son auteur et via son lien de partage.', $build->getName()));

        return $this->backToList($request, 'admin_builds');
    }

    #[Route('/builds/{id}/delete', name: 'admin_build_delete', methods: ['POST'])]
    public function delete(Build $build, Request $request): Response
    {
        if ($error = $this->csrfError($request, 'admin_build_delete', 'admin_builds')) {
            return $error;
        }

        $name = $build->getName();
        $this->entityManager->remove($build); // votes follow via DB CASCADE
        $this->entityManager->flush();
        $this->addFlash('success', sprintf('Build « %s » supprimé.', $name));

        return $this->backToList($request, 'admin_builds');
    }

    /**
     * Row view-models with the net score zipped in (one aggregated query) —
     * built here so the template never indexes an int-keyed PHP map.
     *
     * @param list<Build> $builds
     * @return list<array{build: Build, score: int}>
     */
    private function rows(array $builds): array
    {
        $ids = array_map(static fn (Build $build): int => (int) $build->getId(), $builds);
        $scores = $this->votes->scoreFor($ids);

        return array_map(static fn (Build $build): array => [
            'build' => $build,
            'score' => $scores[(int) $build->getId()] ?? 0,
        ], $builds);
    }
}
