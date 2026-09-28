<?php
declare(strict_types=1);

namespace App\Service\Mail;

/**
 * PII-free description of a failed delivery, for the `mail` channel.
 *
 * The documented carrier for an exception is the object under the key
 * `exception` ({@see docs/guides/logging.md}). The outbound mail path is the one
 * deliberate exception to that rule, because the message of a mailer exception
 * is not ours:
 *
 * - `SmtpTransport::assertResponseCode()` embeds the RAW relay reply, and a
 *   rejection echoes the envelope recipient back verbatim
 *   (`550 5.1.1 <player@example.com>: Recipient address rejected`);
 * - `EsmtpTransport` embeds the MAILER_DSN username in an auth failure.
 *
 * Monolog's `NormalizerFormatter` writes that message unchanged, so `exception`
 * would put an address — or a credential — into an index that is searchable for
 * 90 days, outside the retention this project declares. The prohibition on
 * personal data outranks the convention on how to carry an exception.
 *
 * What is kept still identifies the failure precisely: the class separates a
 * dead relay from a refused recipient, and `file:line` separates the SMTP
 * command that failed (RCPT TO, AUTH, connect). Only the free text is dropped —
 * the relay's own logs hold it, where it legitimately belongs.
 */
final class DeliveryFailure
{
    /** @return array<string, scalar|null> */
    public static function context(\Throwable $e): array
    {
        return [
            'exception_class' => $e::class,
            'exception_code' => $e->getCode(),
            'exception_at' => $e->getFile() . ':' . $e->getLine(),
        ];
    }
}
