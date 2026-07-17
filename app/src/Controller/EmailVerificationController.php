<?php
declare(strict_types=1);

namespace App\Controller;

use App\Entity\User;
use App\Repository\UserRepository;
use App\Service\Audit\AuditAction;
use App\Service\Audit\AuditLogger;
use App\Service\Audit\AuditTarget;
use App\Service\Client\ClientManager;
use App\Service\Client\PageContextResolver;
use App\Service\Client\VersionManager;
use App\Service\Mail\AuthMailer;
use Doctrine\ORM\EntityManagerInterface;
use Symfony\Component\HttpFoundation\Request;
use Symfony\Component\HttpFoundation\RequestStack;
use Symfony\Component\HttpFoundation\Response;
use Symfony\Component\RateLimiter\RateLimiterFactoryInterface;
use Symfony\Component\Routing\Attribute\Route;
use Symfony\Component\Security\Http\Attribute\IsGranted;
use Symfony\Contracts\Translation\TranslatorInterface;
use SymfonyCasts\Bundle\VerifyEmail\Exception\VerifyEmailExceptionInterface;
use SymfonyCasts\Bundle\VerifyEmail\VerifyEmailHelperInterface;

/**
 * Email confirmation: validates the signed link sent at registration and lets a
 * still-unverified, logged-in user re-request it (banner action, rate-limited).
 * Login is never blocked on verification — build creation is (see BuildController).
 */
final class EmailVerificationController extends AbstractResourceController
{
    private const CSRF_RESEND = 'verify-resend';

    public function __construct(
        VersionManager $versionManager,
        ClientManager $clientManager,
        PageContextResolver $pageContext,
        RequestStack $requestStack,
        private readonly VerifyEmailHelperInterface $verifyEmailHelper,
        private readonly UserRepository $users,
        private readonly EntityManagerInterface $entityManager,
        private readonly AuthMailer $authMailer,
        private readonly TranslatorInterface $translator,
        private readonly RateLimiterFactoryInterface $emailVerificationLimiter,
        private readonly AuditLogger $audit,
    ) {
        parent::__construct($versionManager, $clientManager, $pageContext, $requestStack);
    }

    #[Route('/verify/email', name: 'app_verify_email', methods: ['GET'])]
    public function verify(Request $request): Response
    {
        $id = $request->query->get('id');
        $user = $id === null ? null : $this->users->find((int) $id);
        if (!$user instanceof User) {
            return $this->redirectAfterVerify('error', 'auth.flash.verify_error');
        }
        if ($user->isVerified()) {
            return $this->redirectAfterVerify('info', 'auth.flash.verify_already');
        }

        try {
            // The signed URL carries the user id + email; a tampered or stale link throws.
            $this->verifyEmailHelper->validateEmailConfirmationFromRequest($request, (string) $user->getId(), $user->getEmail());
        } catch (VerifyEmailExceptionInterface) {
            return $this->redirectAfterVerify('error', 'auth.flash.verify_error');
        }

        $user->setIsVerified(true);
        $this->entityManager->flush();
        $this->audit->log(AuditAction::UserEmailVerified, target: AuditTarget::user($user));

        return $this->redirectAfterVerify('success', 'auth.flash.verify_success');
    }

    #[Route('/verify/resend', name: 'app_verify_resend', methods: ['POST'])]
    #[IsGranted('ROLE_USER')]
    public function resend(Request $request): Response
    {
        /** @var User $user */
        $user = $this->getUser();

        if (!$this->isCsrfTokenValid(self::CSRF_RESEND, (string) $request->request->get('_token'))) {
            $this->addFlash('error', $this->translator->trans('build.error.csrf'));

            return $this->safeBack($request);
        }
        if ($user->isVerified()) {
            $this->addFlash('info', $this->translator->trans('auth.flash.verify_already'));

            return $this->safeBack($request);
        }
        if (!$this->emailVerificationLimiter->create($user->getUserIdentifier())->consume()->isAccepted()) {
            $this->addFlash('warning', $this->translator->trans('auth.verify.resend_throttled'));

            return $this->safeBack($request);
        }

        $this->authMailer->sendEmailConfirmation($user);
        $this->addFlash('success', $this->translator->trans('auth.verify.resend_done'));

        return $this->safeBack($request);
    }

    private function redirectAfterVerify(string $type, string $messageKey): Response
    {
        $this->addFlash($type, $this->translator->trans($messageKey));

        return $this->redirectToRoute($this->getUser() instanceof User ? 'app_profile' : 'app_login');
    }

    /** Bounce back to the originating page when it is same-origin, else the profile. */
    private function safeBack(Request $request): Response
    {
        $referer = (string) $request->headers->get('referer');
        if ($referer !== '' && str_starts_with($referer, $request->getSchemeAndHttpHost())) {
            return $this->redirect($referer);
        }

        return $this->redirectToRoute('app_profile');
    }
}
