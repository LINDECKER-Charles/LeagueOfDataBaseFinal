<?php
declare(strict_types=1);

namespace App\Controller\Admin;

use Symfony\Bundle\FrameworkBundle\Controller\AbstractController;
use Symfony\Component\HttpFoundation\RedirectResponse;
use Symfony\Component\HttpFoundation\Request;

/**
 * Shared plumbing of the /admin management pages: CSRF validation for POST
 * actions (same convention as the analytics rollup) and redirects preserving
 * the caller's list context (search/filter/page) across mutations.
 */
abstract class AbstractAdminController extends AbstractController
{
    protected const PER_PAGE = 25;
    /** Query params carrying the list context, echoed through action redirects. */
    private const LIST_PARAMS = ['q', 'page', 'visibility'];

    /** Null when the POSTed token is valid; otherwise the error redirect to return. */
    protected function csrfError(Request $request, string $tokenId, string $listRoute): ?RedirectResponse
    {
        if ($this->isCsrfTokenValid($tokenId, (string) $request->request->get('_token'))) {
            return null;
        }
        $this->addFlash('error', 'Jeton CSRF invalide.');

        return $this->backToList($request, $listRoute);
    }

    protected function backToList(Request $request, string $listRoute): RedirectResponse
    {
        $context = array_intersect_key($request->query->all(), array_flip(self::LIST_PARAMS));

        return $this->redirectToRoute($listRoute, array_filter($context, is_scalar(...)));
    }

    protected function pageParam(Request $request): int
    {
        return max(1, $request->query->getInt('page', 1));
    }

    protected function pageCount(int $total): int
    {
        return max(1, (int) ceil($total / static::PER_PAGE));
    }
}
