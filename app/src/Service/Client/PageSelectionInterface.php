<?php
declare(strict_types=1);

namespace App\Service\Client;

/**
 * The (version, lang) a page is being rendered for — the one resolution the
 * controller used ({@see PageContextResolver}), exposed narrowly to the
 * collaborators that only need to read it.
 */
interface PageSelectionInterface
{
    /** @return array{version: string, lang: string} */
    public function selection(): array;
}
