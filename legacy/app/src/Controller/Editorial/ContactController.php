<?php
declare(strict_types=1);

namespace App\Controller\Editorial;

use App\Controller\Concern\BouncesBackSafely;
use App\Controller\Concern\ThrottlesByIp;
use App\Dto\ContactSubmission;
use App\Entity\Enum\ContactCategory;
use App\Entity\ContactMessage;
use App\Entity\User;
use App\Repository\ContactMessageRepository;
use App\Service\Mail\ContactMailer;
use Symfony\Bundle\FrameworkBundle\Controller\AbstractController;
use Symfony\Component\HttpFoundation\Request;
use Symfony\Component\HttpFoundation\Response;
use Symfony\Component\RateLimiter\RateLimiterFactoryInterface;
use Symfony\Component\Routing\Attribute\Route;
use Symfony\Contracts\Translation\TranslatorInterface;
use Symfony\Component\Validator\Validator\ValidatorInterface;

/**
 * Footer contact form endpoint (bug/feedback/review/sales). POST-only: the form
 * lives in the footer on every page, so submission bounces back to the origin
 * page with a flash rather than rendering a dedicated view. Persisted for the
 * /admin inbox and forwarded to CONTACT_RECIPIENT ({@see ContactMailer}).
 */
final class ContactController extends AbstractController
{
    use BouncesBackSafely;
    use ThrottlesByIp;

    private const CSRF_TOKEN_ID = 'contact';
    private const HONEYPOT_FIELD = 'website';

    public function __construct(
        private readonly ContactMessageRepository $messages,
        private readonly ContactMailer $mailer,
        private readonly ValidatorInterface $validator,
        private readonly TranslatorInterface $translator,
        private readonly RateLimiterFactoryInterface $contactFormLimiter,
    ) {}

    #[Route('/contact', name: 'app_contact_submit', methods: ['POST'])]
    public function submit(Request $request): Response
    {
        $back = $this->safeBack($request);
        if ($answer = $this->abuseGate($request, $back)) {
            return $answer;
        }

        $submission = $this->buildSubmission($request);
        if ($error = $this->firstViolation($submission)) {
            return $this->fail($back, $error);
        }

        $message = new ContactMessage(
            $submission,
            $request->getClientIp(),
            $this->authenticatedUserOrNull(),
        );
        $this->messages->save($message);
        $this->notify($message);

        return $this->ok($back);
    }

    /**
     * The three answers that end the request before anything is persisted or sent:
     * a bad CSRF token, a tripped honeypot and a throttled client. Null lets the
     * submission through.
     */
    private function abuseGate(Request $request, Response $back): ?Response
    {
        if (!$this->isCsrfTokenValid(
            self::CSRF_TOKEN_ID,
            (string) $request->request->get('_token'),
        )) {
            return $this->fail($back, 'contact.flash.error');
        }
        // Silent accept on a tripped honeypot: acknowledge like a success so a bot
        // gets no signal, but persist/send nothing.
        if (trim((string) $request->request->get(self::HONEYPOT_FIELD, '')) !== '') {
            return $this->ok($back);
        }
        if ($this->isRateLimited($this->contactFormLimiter, $request)) {
            return $this->fail($back, 'contact.flash.throttled');
        }

        return null;
    }

    private function buildSubmission(Request $request): ContactSubmission
    {
        $data = $request->request;

        return new ContactSubmission(
            category: ContactCategory::tryFrom((string) $data->get('category', '')),
            email: trim((string) $data->get('email', '')),
            message: trim((string) $data->get('message', '')),
            name: $this->nullableTrim($data->get('name')),
            subject: $this->nullableTrim($data->get('subject')),
            locale: $request->getLocale(),
        );
    }

    /** First constraint violation as a translation key, or null when valid. */
    private function firstViolation(ContactSubmission $submission): ?string
    {
        $violations = $this->validator->validate($submission);

        return count($violations) > 0 ? (string) $violations[0]->getMessage() : null;
    }

    /** Mail failure must not fail the request: the message is already persisted. */
    private function notify(ContactMessage $message): void
    {
        try {
            $this->mailer->sendNotification($message);
        } catch (\Throwable) {
            // Swallowed: the /admin inbox holds the message even if the relay is down.
        }
    }

    /**
     * The form is open to anonymous visitors, so a missing identity is normal —
     * distinct from {@see \App\Controller\Concern\ResolvesCurrentUser::currentUser()},
     * which denies access when nobody is signed in.
     */
    private function authenticatedUserOrNull(): ?User
    {
        $user = $this->getUser();

        return $user instanceof User ? $user : null;
    }

    private function ok(Response $back): Response
    {
        $this->addFlash('success', $this->translator->trans('contact.flash.sent'));

        return $back;
    }

    private function fail(Response $back, string $messageKey): Response
    {
        $this->addFlash('error', $this->translator->trans($messageKey));

        return $back;
    }

    private function nullableTrim(mixed $value): ?string
    {
        $value = trim((string) $value);

        return $value === '' ? null : $value;
    }

    /** Bounce to the posted local path, else the same-origin referer, else home. */
    private function safeBack(Request $request): Response
    {
        // The footer form is rendered on every page and posts its own origin, so
        // the dialog can reopen exactly where it was even without a Referer.
        $target = (string) $request->request->get('_redirect', '');
        if (
            str_starts_with($target, '/')
            && !str_starts_with($target, '//')
            && !str_starts_with($target, '/\\')
        ) {
            return $this->redirect($target);
        }

        return $this->backToOrigin($request, 'app_home');
    }
}
