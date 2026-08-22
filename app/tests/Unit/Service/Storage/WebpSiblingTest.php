<?php
declare(strict_types=1);

namespace App\Tests\Unit\Service\Storage;

use App\Service\Storage\WebpSibling;
use PHPUnit\Framework\TestCase;

/**
 * Only PNG blobs are guaranteed a WebP twin (SVG never transcodes): deriving
 * one for anything else would point the browser at a file that was never
 * written — the silent 404 inside a <picture>.
 */
final class WebpSiblingTest extends TestCase
{
    public function testDerivesTheTwinOfAPngKeepingThePathForm(): void
    {
        self::assertSame('cdn/blobs/abc.webp', WebpSibling::of('cdn/blobs/abc.png'));
        self::assertSame('/cdn/blobs/abc.webp', WebpSibling::of('/cdn/blobs/abc.png'));
    }

    public function testAnswersNullForAnythingButAPng(): void
    {
        self::assertNull(WebpSibling::of('cdn/blobs/abc.svg'));
        self::assertNull(WebpSibling::of('cdn/blobs/abc.jpg'));
        self::assertNull(WebpSibling::of('cdn/blobs/abc.PNG'));
        self::assertNull(WebpSibling::of(''));
    }
}
