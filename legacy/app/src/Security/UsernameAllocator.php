<?php
declare(strict_types=1);

namespace App\Security;

use App\Entity\User;
use Symfony\Component\String\Slugger\SluggerInterface;

/**
 * Derives a free, URL-safe username (User::USERNAME_PATTERN) from OAuth profile
 * hints (given name, email local part). Pure logic: the caller supplies the
 * "is taken" predicate, so collision policy stays unit-testable without Doctrine.
 */
final class UsernameAllocator
{
    private const FALLBACK = 'Summoner';
    // Beyond sequential probing, jump to random suffixes to guarantee termination.
    private const MAX_SEQUENTIAL_PROBES = 50;
    // 6-digit tail: a space wide enough that the random branch terminates in a
    // draw or two even once every short suffix is taken.
    private const RANDOM_SUFFIX_MIN = 100_000;
    private const RANDOM_SUFFIX_MAX = 999_999;

    public function __construct(private readonly SluggerInterface $slugger)
    {
    }

    /**
     * @param list<string> $hints candidate names, ordered by preference
     * @param callable(string): bool $isTaken
     */
    public function allocate(array $hints, callable $isTaken): string
    {
        $base = $this->firstUsableBase($hints);
        if (!$isTaken($base)) {
            return $base;
        }

        for ($suffix = 2; $suffix <= self::MAX_SEQUENTIAL_PROBES; ++$suffix) {
            $candidate = $this->withSuffix($base, (string) $suffix);
            if (!$isTaken($candidate)) {
                return $candidate;
            }
        }

        do {
            $candidate = $this->withSuffix(
                $base,
                (string) random_int(self::RANDOM_SUFFIX_MIN, self::RANDOM_SUFFIX_MAX),
            );
        } while ($isTaken($candidate));

        return $candidate;
    }

    /** @param list<string> $hints */
    private function firstUsableBase(array $hints): string
    {
        foreach ($hints as $hint) {
            $base = $this->normalize($hint);
            if ($base !== null) {
                return $base;
            }
        }

        return self::FALLBACK;
    }

    /** Transliterate + strip until the hint fits USERNAME_PATTERN, or null if hopeless. */
    private function normalize(string $hint): ?string
    {
        $slug = $this->slugger->slug(trim($hint))->toString();
        // The slugger already yields [A-Za-z0-9-]; drop leading separators so the
        // first character is alphanumeric, then cap the length.
        $slug = ltrim($slug, '-_.');
        $slug = mb_substr($slug, 0, User::USERNAME_MAX_LENGTH);

        $isValid = mb_strlen($slug) >= User::USERNAME_MIN_LENGTH
            && preg_match(User::USERNAME_PATTERN, $slug) === 1;

        return $isValid ? $slug : null;
    }

    /** Append the suffix, shortening the base so the result still fits the column. */
    private function withSuffix(string $base, string $suffix): string
    {
        return mb_substr($base, 0, User::USERNAME_MAX_LENGTH - mb_strlen($suffix)).$suffix;
    }
}
