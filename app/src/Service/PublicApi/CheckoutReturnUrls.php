<?php
declare(strict_types=1);

namespace App\Service\PublicApi;

/** Absolute success/cancel URLs of a Checkout Session, grouped to keep signatures small. */
final readonly class CheckoutReturnUrls
{
    public function __construct(
        public string $success,
        public string $cancel,
    ) {}
}
