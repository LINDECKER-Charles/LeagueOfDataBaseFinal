<?php
declare(strict_types=1);

namespace App\Controller\Admin;

use App\Entity\User;
use App\Repository\UserRepository;
use App\Service\Audit\AuditAction;
use App\Service\Audit\AuditLogger;
use App\Service\Audit\AuditTarget;
use Doctrine\ORM\EntityManagerInterface;
use Symfony\Component\HttpFoundation\Request;
use Symfony\Component\HttpFoundation\Response;
use Symfony\Component\Routing\Attribute\Route;

/**
 * Account moderation: search, ban/unban (login block + public surfaces hidden)
 * and hard delete (builds/votes/keys cascade, donations detach — DB-enforced).
 * ROLE_ADMIN via the /admin firewall; every mutation is a CSRF-checked POST.
 */
#[Route('/admin')]
final class UserModerationController extends AbstractAdminController
{
    public function __construct(
        private readonly UserRepository $users,
        private readonly EntityManagerInterface $entityManager,
        private readonly AuditLogger $audit,
    ) {}

    #[Route('/users', name: 'admin_users', methods: ['GET'])]
    public function index(Request $request): Response
    {
        $query = trim((string) $request->query->get('q', ''));
        $page = $this->pageParam($request);
        ['rows' => $rows, 'total' => $total] = $this->users->searchPaginated($query, $page, self::PER_PAGE);

        return $this->render('admin/users.html.twig', [
            'rows' => $rows,
            'total' => $total,
            'q' => $query,
            'page' => $page,
            'pages' => $this->pageCount($total),
            'stats' => [
                'total' => $this->users->countAll(),
                'newWeek' => $this->users->countNewSince(new \DateTimeImmutable('-7 days')),
                'banned' => $this->users->countBanned(),
                'supporters' => $this->users->countSupporters(),
            ],
        ]);
    }

    #[Route('/users/{id}/ban', name: 'admin_user_ban', methods: ['POST'])]
    public function ban(User $user, Request $request): Response
    {
        if ($error = $this->csrfError($request, 'admin_user_ban', 'admin_users')) {
            return $error;
        }

        $reason = trim((string) $request->request->get('reason', ''));
        $user->ban($reason === '' ? null : $reason);
        $this->entityManager->flush();
        $this->audit->log(AuditAction::AdminUserBan, target: AuditTarget::user($user), metadata: ['reason' => $reason === '' ? null : $reason]);
        $this->addFlash('success', sprintf('Compte « %s » banni : connexion bloquée, profil et builds retirés du site public.', $user->getUsername()));

        return $this->backToList($request, 'admin_users');
    }

    #[Route('/users/{id}/unban', name: 'admin_user_unban', methods: ['POST'])]
    public function unban(User $user, Request $request): Response
    {
        if ($error = $this->csrfError($request, 'admin_user_unban', 'admin_users')) {
            return $error;
        }

        $user->unban();
        $this->entityManager->flush();
        $this->audit->log(AuditAction::AdminUserUnban, target: AuditTarget::user($user));
        $this->addFlash('success', sprintf('Compte « %s » rétabli.', $user->getUsername()));

        return $this->backToList($request, 'admin_users');
    }

    #[Route('/users/{id}/delete', name: 'admin_user_delete', methods: ['POST'])]
    public function delete(User $user, Request $request): Response
    {
        if ($error = $this->csrfError($request, 'admin_user_delete', 'admin_users')) {
            return $error;
        }

        $username = $user->getUsername();
        // Capture the audit target before removal — the id is needed after flush.
        $target = AuditTarget::user($user);
        // DB-level cascades do the fan-out: builds/votes/api_keys CASCADE,
        // donations SET NULL (accounting lines survive account deletion).
        $this->entityManager->remove($user);
        $this->entityManager->flush();
        $this->audit->log(AuditAction::AdminUserDelete, target: $target);
        $this->addFlash('success', sprintf('Compte « %s » supprimé définitivement.', $username));

        return $this->backToList($request, 'admin_users');
    }
}
