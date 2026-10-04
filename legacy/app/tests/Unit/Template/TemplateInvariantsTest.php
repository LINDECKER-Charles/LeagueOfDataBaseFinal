<?php

declare(strict_types=1);

namespace App\Tests\Unit\Template;

use PHPUnit\Framework\TestCase;
use Symfony\Component\Finder\Finder;

/**
 * Invariants of the view layer that no rendered assertion can catch, because the
 * damage they describe is a slow drift: a rule copied into a new template today
 * and edited in only one place tomorrow. Both were live defects before this
 * suite existed.
 */
final class TemplateInvariantsTest extends TestCase
{
    /**
     * base.html.twig deliberately reads the SESSION for its dd-* metas: those are
     * the streaming loader's fallback for a destination URL carrying neither
     * version nor lang — and such a destination does render from the session.
     */
    private const SESSION_READERS = ['base.html.twig'];

    /** The sibling-WebP guard lives in exactly one partial. */
    private const WEBP_OWNER = 'components/_webp_source.html.twig';

    /**
     * The rendered (version, lang) has a single source, page_selection()
     * (PageContextResolver: path > ?version= > session). Re-deriving it from the
     * session ignores the query and the /{version}/… path, so a page shows one
     * patch in its banner and renders another.
     */
    public function testOnlyTheDocumentedExceptionReadsTheSessionSelection(): void
    {
        self::assertSame(
            self::SESSION_READERS,
            $this->templatesContaining('client.session.'),
        );
    }

    /**
     * "Every .png blob has a .webp twin" is a storage invariant, not a template
     * detail: emitting the <source> without the .png guard yields a silent 404
     * inside the <picture>, which is exactly how the copies had drifted.
     */
    public function testTheWebpSiblingRuleLivesInASinglePartial(): void
    {
        self::assertSame(
            [self::WEBP_OWNER],
            $this->templatesContaining('type="image/webp"'),
        );
    }

    /** @return list<string> matching template paths, relative to templates/, sorted */
    private function templatesContaining(string $needle): array
    {
        $root = \dirname(__DIR__, 3) . '/templates';
        $matches = [];

        foreach (Finder::create()->files()->in($root)->name('*.twig') as $file) {
            if (str_contains((string) file_get_contents($file->getPathname()), $needle)) {
                $matches[] = str_replace('\\', '/', $file->getRelativePathname());
            }
        }

        sort($matches);

        return $matches;
    }
}
