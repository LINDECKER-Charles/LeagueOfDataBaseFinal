<?php
declare(strict_types=1);

namespace App\Tests\Unit\Service\Tools;

use App\Service\Tools\UrlGenerator;
use PHPUnit\Framework\TestCase;
use Symfony\Component\HttpFoundation\RequestStack;
use Symfony\Component\Routing\Generator\UrlGeneratorInterface;

/**
 * applySelection() must place the new version where {@see PageContextResolver}
 * precedence (path > query > session) will actually honor it — a versioned path
 * segment outranks a ?version= query, so switching version on `/{version}/…` must
 * rewrite the segment, not just append a query that the old segment shadows.
 */
final class UrlGeneratorApplySelectionTest extends TestCase
{
    private UrlGenerator $urls;

    protected function setUp(): void
    {
        // applySelection() is pure — the router is never touched here, so a stub
        // (no expectation verification) is the right double.
        $this->urls = new UrlGenerator(new RequestStack(), $this->createStub(UrlGeneratorInterface::class));
    }

    public function testVersionedPathSegmentIsRewrittenNotQueried(): void
    {
        self::assertSame(
            '/14.9.1/champion/Akali?lang=en_US',
            $this->urls->applySelection('http://localhost:8080/10.13.1/champion/Akali?lang=en_US', '14.9.1', 'en_US'),
        );
    }

    public function testVersionedListPathSegmentIsRewritten(): void
    {
        self::assertSame(
            '/14.9.1/champions?lang=fr_FR',
            $this->urls->applySelection('http://localhost:8080/10.13.1/champions?lang=en_US', '14.9.1', 'fr_FR'),
        );
    }

    public function testQueryVersionIsReplacedOnCleanPath(): void
    {
        self::assertSame(
            '/champion/Akali?version=14.9.1&lang=fr_FR',
            $this->urls->applySelection('http://localhost:8080/champion/Akali?version=10.13.1&lang=en_US', '14.9.1', 'fr_FR'),
        );
    }

    public function testVersionAddedToQueryWhenAbsentEverywhere(): void
    {
        self::assertSame(
            '/champion/Akali?version=14.9.1&lang=en_US',
            $this->urls->applySelection('http://localhost:8080/champion/Akali', '14.9.1', 'en_US'),
        );
    }

    public function testNumericResourceIdIsNotMistakenForAVersionSegment(): void
    {
        // "1001" has no dotted form → not a version → version goes to the query.
        self::assertSame(
            '/object/1001?version=14.9.1&lang=en_US',
            $this->urls->applySelection('http://localhost:8080/object/1001?version=10.13.1&lang=en_US', '14.9.1', 'en_US'),
        );
    }

    public function testUnrelatedQueryAndFragmentArePreserved(): void
    {
        self::assertSame(
            '/champion/Akali?foo=bar&version=14.9.1&lang=en_US#skins',
            $this->urls->applySelection('http://localhost:8080/champion/Akali?foo=bar#skins', '14.9.1', 'en_US'),
        );
    }

    public function testInertPathIsLeftUntouched(): void
    {
        $url = 'http://localhost:8080/working-progress?version=10.13.1';
        self::assertSame($url, $this->urls->applySelection($url, '14.9.1', 'en_US'));
    }
}
