<?php
declare(strict_types=1);

namespace App\Service\Mail;

/**
 * The account-lifecycle emails, each binding its subject translation key to its
 * Twig template pair. Subject and template always change together, so they are
 * declared side by side here rather than as parallel constants a caller has to
 * pair up correctly.
 */
enum AuthMailTemplate
{
    case Confirmation;
    case PasswordReset;

    public function subjectKey(): string
    {
        return match ($this) {
            self::Confirmation => 'email.confirm.subject',
            self::PasswordReset => 'email.reset.subject',
        };
    }

    /** Template path without extension — the HTML and text pair share it. */
    public function path(): string
    {
        return match ($this) {
            self::Confirmation => 'email/confirmation',
            self::PasswordReset => 'email/reset_password',
        };
    }
}
