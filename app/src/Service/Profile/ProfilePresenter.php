<?php
declare(strict_types=1);

namespace App\Service\Profile;

/**
 * Identity presentation helpers of the profile pages: enough to recognise
 * yourself, nothing for a shoulder-surfer to harvest.
 */
final class ProfilePresenter
{
    private const MASK = '***';

    /** "charles@outlook.fr" → "c***@outlook.fr". */
    public function maskEmail(string $email): string
    {
        $at = mb_strpos($email, '@');
        if ($at === false || $at < 1) {
            return self::MASK;
        }

        return mb_substr($email, 0, 1).self::MASK.mb_substr($email, $at);
    }

    /** Month + year in the UI locale ("juillet 2026"); numeric fallback when ICU can't. */
    public function memberSince(\DateTimeImmutable $createdAt, string $locale): string
    {
        try {
            $formatter = new \IntlDateFormatter($locale, \IntlDateFormatter::NONE, \IntlDateFormatter::NONE, null, null, 'LLLL yyyy');
            $formatted = $formatter->format($createdAt);
            if (\is_string($formatted) && $formatted !== '') {
                return $formatted;
            }
        } catch (\Throwable) {
            // symfony/intl's polyfill only speaks "en" — fall through, don't break the page.
        }

        return $createdAt->format('m/Y');
    }
}
