<?php
declare(strict_types=1);

namespace App\Controller\Admin;

use App\Entity\ContactMessage;
use App\Entity\ContactStatus;
use App\Repository\ContactMessageRepository;
use Doctrine\ORM\EntityManagerInterface;
use Symfony\Component\HttpFoundation\RedirectResponse;
use Symfony\Component\HttpFoundation\Request;
use Symfony\Component\HttpFoundation\Response;
use Symfony\Component\Routing\Attribute\Route;

/**
 * Contact inbox: the operator-facing view of footer submissions, with triage
 * (mark handled / reopen) and spam deletion. ROLE_ADMIN via the /admin firewall;
 * every mutation is a CSRF-checked POST that preserves the list context.
 */
#[Route('/admin')]
final class ContactModerationController extends AbstractAdminController
{
    public function __construct(
        private readonly ContactMessageRepository $messages,
        private readonly EntityManagerInterface $entityManager,
    ) {}

    #[Route('/contacts', name: 'admin_contacts', methods: ['GET'])]
    public function index(Request $request): Response
    {
        $status = ContactStatus::tryFrom((string) $request->query->get('status', ''));
        $page = $this->pageParam($request);
        ['rows' => $rows, 'total' => $total] = $this->messages->page($page, self::PER_PAGE, $status);

        return $this->render('admin/contacts.html.twig', [
            'rows' => $rows,
            'total' => $total,
            'status' => $status,
            'page' => $page,
            'pages' => $this->pageCount($total),
            'stats' => [
                'total' => $this->messages->count([]),
                'new' => $this->messages->countByStatus(ContactStatus::New),
                'handled' => $this->messages->countByStatus(ContactStatus::Handled),
                'week' => $this->messages->countSince(new \DateTimeImmutable('-7 days')),
            ],
        ]);
    }

    #[Route('/contacts/{id}/handle', name: 'admin_contact_handle', methods: ['POST'])]
    public function handle(ContactMessage $message, Request $request): Response
    {
        if ($error = $this->csrfError($request, 'admin_contact_handle', 'admin_contacts')) {
            return $error;
        }

        $message->markHandled();
        $this->entityManager->flush();
        $this->addFlash('success', 'Message marqué comme traité.');

        return $this->backToInbox($request);
    }

    #[Route('/contacts/{id}/reopen', name: 'admin_contact_reopen', methods: ['POST'])]
    public function reopen(ContactMessage $message, Request $request): Response
    {
        if ($error = $this->csrfError($request, 'admin_contact_reopen', 'admin_contacts')) {
            return $error;
        }

        $message->reopen();
        $this->entityManager->flush();
        $this->addFlash('success', 'Message rouvert.');

        return $this->backToInbox($request);
    }

    #[Route('/contacts/{id}/delete', name: 'admin_contact_delete', methods: ['POST'])]
    public function delete(ContactMessage $message, Request $request): Response
    {
        if ($error = $this->csrfError($request, 'admin_contact_delete', 'admin_contacts')) {
            return $error;
        }

        $this->entityManager->remove($message);
        $this->entityManager->flush();
        $this->addFlash('success', 'Message supprimé définitivement.');

        return $this->backToInbox($request);
    }

    /** Preserve the status filter + page across a triage mutation. */
    private function backToInbox(Request $request): RedirectResponse
    {
        $context = array_filter([
            'status' => (string) $request->query->get('status', ''),
            'page' => (string) $request->query->get('page', ''),
        ], static fn (string $v): bool => $v !== '');

        return $this->redirectToRoute('admin_contacts', $context);
    }
}
