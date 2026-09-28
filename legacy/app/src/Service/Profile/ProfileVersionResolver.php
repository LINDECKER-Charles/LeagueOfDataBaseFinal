<?php
declare(strict_types=1);

namespace App\Service\Profile;

use App\Entity\User;
use App\Service\Client\VersionManager;

/**
 * Resolves the Data Dragon patch a profile's favorites are shown and saved at.
 * The owner may pin a preferred version (so a favorite never vanishes or gets
 * wiped when the browsing version lacks it); a pin that no longer exists upstream
 * is ignored and the browsing version is used instead.
 */
final class ProfileVersionResolver
{
    public function __construct(private readonly VersionManager $versions) {}

    public function effective(User $user, string $browsingVersion): string
    {
        $preferred = $user->getPreferredVersion();

        return $preferred !== null && $this->versions->versionExists($preferred)
            ? $preferred
            : $browsingVersion;
    }
}
