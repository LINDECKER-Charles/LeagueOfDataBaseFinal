<?php
declare(strict_types=1);

namespace App\Tests\Unit\Service\Seo;

use App\Service\Seo\CanonicalHost;
use PHPUnit\Framework\TestCase;

final class CanonicalHostTest extends TestCase
{
    private const PREFERRED = 'league-of-data-base.com';
    private const MIRROR = 'league-of-data-base.fr';

    private CanonicalHost $hosts;

    protected function setUp(): void
    {
        $this->hosts = new CanonicalHost(self::PREFERRED, [self::PREFERRED, self::MIRROR]);
    }

    public function testWwwCollapsesIntoTheApex(): void
    {
        self::assertSame(self::PREFERRED, $this->hosts->resolve('www.' . self::PREFERRED));
    }

    public function testMirrorDomainFoldsIntoThePreferredHost(): void
    {
        // .fr serves identical content in the same language — it must not compete.
        self::assertSame(self::PREFERRED, $this->hosts->resolve(self::MIRROR));
        self::assertSame(self::PREFERRED, $this->hosts->resolve('www.' . self::MIRROR));
    }

    public function testUnknownHostStaysSelfCanonical(): void
    {
        // Dev, staging and preview environments must never point at production.
        self::assertSame('localhost', $this->hosts->resolve('localhost'));
        self::assertSame('test.example.com', $this->hosts->resolve('test.example.com'));
    }

    public function testWwwStillCollapsesWhenFoldingIsDisabled(): void
    {
        // With no preferred host, mirrors stay themselves — but www never does:
        // it is the same site by any measure.
        $hosts = new CanonicalHost('', []);

        self::assertSame(self::MIRROR, $hosts->resolve('www.' . self::MIRROR));
        self::assertSame(self::MIRROR, $hosts->resolve(self::MIRROR));
    }

    public function testOriginOmitsDefaultPortsAndKeepsOthers(): void
    {
        self::assertSame(
            'https://' . self::PREFERRED,
            $this->hosts->origin('https', self::MIRROR, 443),
        );
        self::assertSame('http://localhost', $this->hosts->origin('http', 'localhost', 80));
        self::assertSame('http://localhost:8080', $this->hosts->origin('http', 'localhost', 8080));
        self::assertSame(
            'https://' . self::PREFERRED,
            $this->hosts->origin('https', 'www.' . self::PREFERRED),
        );
    }
}
