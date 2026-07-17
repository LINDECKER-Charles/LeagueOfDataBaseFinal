<?php
declare(strict_types=1);

namespace App\Controller;

use App\Entity\Build;
use App\Entity\BuildVote;
use App\Entity\User;
use App\Repository\BuildRepository;
use App\Repository\BuildVoteRepository;
use App\Service\Audit\AuditAction;
use App\Service\Audit\AuditLogger;
use App\Service\Audit\AuditTarget;
use Symfony\Bundle\FrameworkBundle\Controller\AbstractController;
use Symfony\Component\HttpFoundation\JsonResponse;
use Symfony\Component\HttpFoundation\RedirectResponse;
use Symfony\Component\HttpFoundation\Request;
use Symfony\Component\HttpFoundation\Response;
use Symfony\Component\Routing\Attribute\Route;
use Symfony\Contracts\Translation\TranslatorInterface;

/**
 * Vote endpoint for public builds — under ^/builds so the existing ROLE_USER
 * access_control guards it. One route, two response shapes (simple content
 * negotiation): the Vue island posts with `Accept: application/json` and gets
 * `{score, myVote}` back; the no-JS forms get redirected to where they came
 * from (Referer, only when it points at this host — otherwise /trends).
 *
 * Only PUBLIC builds are votable; a private build id answers 404 like every
 * other ownership miss, so the endpoint leaks no existence oracle.
 */
final class BuildVoteController extends AbstractController
{
    private const CSRF_TOKEN_ID = 'submit';
    private const DIRECTIONS = ['up' => BuildVote::UP, 'down' => BuildVote::DOWN];

    public function __construct(
        private readonly BuildRepository $builds,
        private readonly BuildVoteRepository $votes,
        private readonly TranslatorInterface $translator,
        private readonly AuditLogger $audit,
    ) {}

    #[Route('/builds/{id}/vote', name: 'app_build_vote', requirements: ['id' => '\d+'], methods: ['POST'])]
    public function vote(Request $request, int $id): Response
    {
        $wantsJson = str_contains((string) $request->headers->get('Accept', ''), 'application/json');

        if (!$this->isCsrfTokenValid(self::CSRF_TOKEN_ID, (string) $request->request->get('_token'))) {
            return $this->reject($request, $wantsJson, 'build.error.csrf', Response::HTTP_FORBIDDEN);
        }
        $value = self::DIRECTIONS[(string) $request->request->get('value')] ?? null;
        if ($value === null) {
            return $this->reject($request, $wantsJson, 'community.vote.invalid', Response::HTTP_BAD_REQUEST);
        }

        $build = $this->builds->find($id);
        if ($build === null || !$build->isPublic()) {
            throw $this->createNotFoundException('Build not found.');
        }

        $voter = $this->currentUser();
        $this->votes->applyVote($build, $voter, $value);
        $this->audit->log(
            AuditAction::BuildVote,
            target: AuditTarget::of(AuditTarget::TYPE_BUILD, $build->getId(), $build->getName()),
            metadata: ['value' => $value],
        );

        return $wantsJson ? new JsonResponse($this->voteState($build, $voter)) : $this->redirectBack($request);
    }

    /** @return array{score: int, myVote: int} */
    private function voteState(Build $build, User $voter): array
    {
        $id = (int) $build->getId();

        return [
            'score' => $this->votes->scoreFor([$id])[$id] ?? 0,
            'myVote' => $this->votes->findOneByBuildAndVoter($build, $voter)?->getValue() ?? 0,
        ];
    }

    private function reject(Request $request, bool $wantsJson, string $messageKey, int $status): Response
    {
        if ($wantsJson) {
            return new JsonResponse(['error' => $this->translator->trans($messageKey)], $status);
        }
        $this->addFlash('error', $this->translator->trans($messageKey));

        return $this->redirectBack($request);
    }

    /**
     * Same-host Referer only — anything else (foreign host, opaque scheme)
     * falls back to /trends so the endpoint can never be an open redirect.
     */
    private function redirectBack(Request $request): RedirectResponse
    {
        $parts = parse_url((string) $request->headers->get('referer', ''));
        if (\is_array($parts) && ($parts['host'] ?? null) === $request->getHost()) {
            $target = ($parts['path'] ?? '/') . (isset($parts['query']) ? '?' . $parts['query'] : '');

            return new RedirectResponse($target, Response::HTTP_SEE_OTHER);
        }

        return $this->redirectToRoute('app_trends', status: Response::HTTP_SEE_OTHER);
    }

    private function currentUser(): User
    {
        $user = $this->getUser();
        if (!$user instanceof User) {
            // access_control guarantees ROLE_USER here; guard against misconfig.
            throw $this->createAccessDeniedException();
        }

        return $user;
    }
}
