<?php
declare(strict_types=1);

namespace App\Controller;

use App\Controller\Concern\ThrottlesByIp;
use App\Dto\ContactSubmission;
use App\Entity\ContactCategory;
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

        if (!$this->isCsrfTokenValid(self::CSRF_TOKEN_ID, (string) $request->request->get('_token'))) {
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

        $submission = $this->buildSubmission($request);
        if ($error = $this->firstViolation($submission)) {
            return $this->fail($back, $error);
        }

        $message = new ContactMessage($submission, $request->getClientIp(), $this->currentUser());
        $this->messages->save($message);
        $this->notify($message);

        return $this->ok($back);
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

    private function currentUser(): ?User
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
        $target = (string) $request->request->get('_redirect', '');
        if (str_starts_with($target, '/') && !str_starts_with($target, '//') && !str_starts_with($target, '/\\')) {
            return $this->redirect($target);
        }

        $referer = (string) $request->headers->get('referer');
        if ($referer !== '' && str_starts_with($referer, $request->getSchemeAndHttpHost())) {
            return $this->redirect($referer);
        }

        return $this->redirectToRoute('app_home');
    }
}
