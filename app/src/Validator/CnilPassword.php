<?php
declare(strict_types=1);

namespace App\Validator;

use Symfony\Component\Validator\Constraint;

/**
 * CNIL password policy — deliberation n° 2022-100 (2022-07-21), the "password
 * only" case: ~80 bits of entropy, i.e. at least 12 characters mixing
 * lowercase, uppercase, digits and special characters — plus the associated
 * requirement of rejecting trivial/common passwords (embedded local denylist,
 * no external call: all PHP egress is forbidden by the architecture).
 *
 * Messages are keys of the shared `messages` translation domain (the project
 * keeps a single hand-maintained catalog per locale); the validator translates
 * them itself instead of relying on the `validators` domain.
 */
#[\Attribute(\Attribute::TARGET_PROPERTY | \Attribute::TARGET_METHOD)]
final class CnilPassword extends Constraint
{
    public const MIN_LENGTH = 12;

    public string $tooShort = 'auth.password.rule_length';
    public string $missingLowercase = 'auth.password.rule_lowercase';
    public string $missingUppercase = 'auth.password.rule_uppercase';
    public string $missingDigit = 'auth.password.rule_digit';
    public string $missingSpecial = 'auth.password.rule_special';
    public string $tooCommon = 'auth.password.too_common';
}
