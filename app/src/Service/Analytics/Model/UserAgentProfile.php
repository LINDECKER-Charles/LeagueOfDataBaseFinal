<?php
declare(strict_types=1);

namespace App\Service\Analytics\Model;

/**
 * What {@see \App\Service\Analytics\UserAgentParser} could tell about a client.
 * A value object rather than an array: the shape is stable and its four fields
 * are copied straight into every {@see \App\Service\Analytics\RequestEvent}, so
 * a mistyped key must be a compile-time error, not a runtime surprise.
 */
final readonly class UserAgentProfile
{
    /** Neither the browser, the OS nor the device family could be identified. */
    public const UNKNOWN = 'other';

    public function __construct(
        public string $browser,
        public string $os,
        public string $device,
        public bool $isBot,
    ) {}

    public static function unknown(): self
    {
        return new self(self::UNKNOWN, self::UNKNOWN, self::UNKNOWN, false);
    }

    /** Bots keep their own device family so audience breakdowns can exclude them. */
    public static function bot(): self
    {
        return new self('Bot', self::UNKNOWN, 'bot', true);
    }
}
