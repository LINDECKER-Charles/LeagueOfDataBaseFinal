<?php
declare(strict_types=1);

namespace App\Service\Mail;

use App\Entity\User;
use Monolog\Attribute\WithMonologChannel;
use Psr\Log\LoggerInterface;
use Symfony\Bridge\Twig\Mime\TemplatedEmail;
use Symfony\Component\Mailer\MailerInterface;
use Symfony\Component\Mime\Address;
use Symfony\Component\Routing\Generator\UrlGeneratorInterface;
use Symfony\Contracts\Translation\TranslatorInterface;
use SymfonyCasts\Bundle\ResetPassword\Model\ResetPasswordToken;
use SymfonyCasts\Bundle\VerifyEmail\VerifyEmailHelperInterface;

/**
 * Single owner of the account-lifecycle emails (email confirmation + password
 * reset). Centralises the sender identity, subject translation and the shared
 * Hextech template pair (HTML + text) so the controllers only decide *when* to
 * send, never *how*. Sent synchronously (see messenger.yaml routing).
 */
#[WithMonologChannel('mail')]
final readonly class AuthMailer
{
    private const CONFIRM_ROUTE = 'app_verify_email';
    private const RESET_ROUTE = 'app_reset_password';

    public function __construct(
        private MailerInterface $mailer,
        private VerifyEmailHelperInterface $verifyEmailHelper,
        private UrlGeneratorInterface $urlGenerator,
        private TranslatorInterface $translator,
        private LoggerInterface $logger,
        private string $sender,
    ) {
    }

    public function sendEmailConfirmation(User $user): void
    {
        $userId = (string) $user->getId();
        $signature = $this->verifyEmailHelper->generateSignature(
            self::CONFIRM_ROUTE,
            $userId,
            $user->getEmail(),
            ['id' => $userId],
        );

        $this->send($user, AuthMailTemplate::Confirmation, [
            'actionUrl' => $signature->getSignedUrl(),
            'expiresAtMessageKey' => $signature->getExpirationMessageKey(),
            'expiresAtMessageData' => $signature->getExpirationMessageData(),
        ]);
    }

    public function sendPasswordReset(User $user, ResetPasswordToken $token): void
    {
        $actionUrl = $this->urlGenerator->generate(
            self::RESET_ROUTE,
            ['token' => $token->getToken()],
            UrlGeneratorInterface::ABSOLUTE_URL,
        );

        $this->send($user, AuthMailTemplate::PasswordReset, [
            'actionUrl' => $actionUrl,
            'expiresAtMessageKey' => $token->getExpirationMessageKey(),
            'expiresAtMessageData' => $token->getExpirationMessageData(),
        ]);
    }

    /** @param array<string, mixed> $context */
    private function send(User $user, AuthMailTemplate $mail, array $context): void
    {
        $template = $mail->path();
        $email = (new TemplatedEmail())
            ->from(Address::create($this->sender))
            ->to(new Address($user->getEmail(), $user->displayName()))
            ->subject($this->translator->trans($mail->subjectKey()))
            ->htmlTemplate($template.'.html.twig')
            ->textTemplate($template.'.txt.twig')
            ->context($context + ['user' => $user]);

        $this->deliver($email, $mail, $user);
    }

    /**
     * Delivery, plus the one thing the call sites cannot do: name the failure.
     * The exception is RE-THROWN unchanged — each caller keeps its own policy
     * (registration swallows it behind a resend banner, the reset flow stays
     * silent so the outcome cannot probe which addresses exist).
     *
     * Context carries the internal user id, never the address: a log line lives
     * in a 90-day searchable index, outside the retention we declare.
     */
    private function deliver(TemplatedEmail $email, AuthMailTemplate $mail, User $user): void
    {
        try {
            $this->mailer->send($email);
        } catch (\Throwable $e) {
            $this->logger->error('mail.send.failed', [
                'kind' => $mail->name,
                'user_id' => $user->getId(),
                'exception' => $e,
            ]);

            throw $e;
        }
    }
}
