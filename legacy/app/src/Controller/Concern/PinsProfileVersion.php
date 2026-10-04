<?php
declare(strict_types=1);

namespace App\Controller\Concern;

use App\Entity\User;

/**
 * The patch a summoner's chamber resolves against. Pinning exists so a favorite
 * absent from the browsing version neither disappears from the page nor gets
 * wiped on the next save — which makes it a rule the rendering side and the
 * writing side must apply identically, hence this single home.
 *
 * For controllers extending {@see \App\Controller\AbstractPageController} that
 * inject a {@see \App\Service\Profile\ProfileVersionResolver} as `$profileVersion`.
 */
trait PinsProfileVersion
{
    /**
     * The browsing selection with the version replaced by the owner's pinned
     * patch, when they pinned one.
     *
     * @return array{version: string, lang: string}
     */
    private function pinnedSelection(User $user): array
    {
        $selection = $this->pageContext->selection();
        $selection['version'] = $this->profileVersion->effective($user, $selection['version']);

        return $selection;
    }
}
