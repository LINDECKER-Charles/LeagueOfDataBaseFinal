<?php
declare(strict_types=1);

namespace App\Controller\Build;

use App\Controller\Concern\BouncesBackSafely;
use App\Controller\Concern\ResolvesCurrentUser;
use App\Entity\Build;
use App\Entity\BuildVote;
use App\Entity\User;
use App\Repository\BuildRepository;
use App\Repository\BuildVoteRepository;
use App\Service\Audit\Model\AuditAction;
use App\Service\Audit\AuditLogger;
use App\Service\Audit\Model\AuditTarget;
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
    use BouncesBackSafely;
    use ResolvesCurrentUser;

    private const CSRF_TOKEN_ID = 'submit';
    private const DIRECTIONS = ['up' => BuildVote::UP, 'down' => BuildVote::DOWN];

    public function __construct(
        private readonly BuildRepository $builds,
        private readonly BuildVoteRepository $votes,
        private readonly TranslatorInterface $translator,
        private readonly AuditLogger $audit,
    ) {}

    #[Route(
        '/builds/{id}/vote',
        name: 'app_build_vote',
        requirements: ['id' => '\d+'],
        methods: ['POST'],
    )]
    public function vote(Request $request, int $id): Response
    {
        if (!$this->isCsrfTokenValid(
            self::CSRF_TOKEN_ID,
            (string) $request->request->get('_token'),
        )) {
            return $this->reject($request, 'build.error.csrf', Response::HTTP_FORBIDDEN);
        }

        $value = self::DIRECTIONS[(string) $request->request->get('value')] ?? null;
        if ($value === null) {
            return $this->reject($request, 'community.vote.invalid', Response::HTTP_BAD_REQUEST);
        }

        $build = $this->votableOr404($id);
        $voter = $this->currentUser();
        $this->votes->applyVote($build, $voter, $value);
        $this->audit->log(
            AuditAction::BuildVote,
            target: AuditTarget::of(AuditTarget::TYPE_BUILD, $build->getId(), $build->getName()),
            metadata: ['value' => $value],
        );

        return $this->wantsJson($request)
            ? new JsonResponse($this->voteState($build, $voter))
            : $this->redirectBack($request);
    }

    /**
     * A public build, or 404 — a private one answers exactly like a missing id,
     * so the endpoint leaks no existence oracle.
     */
    private function votableOr404(int $id): Build
    {
        $build = $this->builds->find($id);
        if ($build === null || !$build->isPublic()) {
            throw $this->createNotFoundException('Build not found.');
        }

        return $build;
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

    /** The Vue island posts with `Accept: application/json`; the no-JS forms do not. */
    private function wantsJson(Request $request): bool
    {
        return str_contains((string) $request->headers->get('Accept', ''), 'application/json');
    }

    /**
     * A refused vote in the shape the caller expects: the status is what the island
     * reads (no flash makes sense for a fetch caller), while a no-JS form gets the
     * message on a flash and lands back on the page it came from.
     */
    private function reject(Request $request, string $messageKey, int $status): Response
    {
        $message = $this->translator->trans($messageKey);
        if ($this->wantsJson($request)) {
            return new JsonResponse(['error' => $message], $status);
        }

        $this->addFlash('error', $message);

        return $this->redirectBack($request);
    }

    /**
     * Same-origin Referer only — anything else (foreign host, opaque scheme)
     * falls back to /trends so the endpoint can never be an open redirect.
     */
    private function redirectBack(Request $request): RedirectResponse
    {
        return $this->backToOrigin($request, 'app_trends', Response::HTTP_SEE_OTHER);
    }
}
