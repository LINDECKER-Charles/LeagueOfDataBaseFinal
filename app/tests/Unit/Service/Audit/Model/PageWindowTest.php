<?php
declare(strict_types=1);

namespace App\Tests\Unit\Service\Audit\Model;

use App\Service\Audit\Model\PageWindow;
use PHPUnit\Framework\TestCase;

final class PageWindowTest extends TestCase
{
    public function testFirstPageStartsAtTheOrigin(): void
    {
        $window = new PageWindow(1, 20);

        self::assertSame(0, $window->offset);
        self::assertSame(21, $window->scanLimit()); // page + the probe row
    }

    public function testPageBelowOneIsClampedToTheFirstPage(): void
    {
        self::assertSame(0, (new PageWindow(0, 20))->offset);
        self::assertSame(0, (new PageWindow(-5, 20))->offset);
    }

    public function testCutReturnsOnlyTheRequestedPage(): void
    {
        $window = new PageWindow(2, 3);

        self::assertSame(['d', 'e', 'f'], $window->cut(range('a', 'i'))['rows']);
    }

    public function testProbeRowIsWhatProvesANextPageExists(): void
    {
        $window = new PageWindow(1, 3);

        // Exactly one page collected: nothing proves there is more.
        self::assertFalse($window->cut(['a', 'b', 'c'])['hasMore']);
        self::assertTrue($window->cut(['a', 'b', 'c', 'd'])['hasMore']);
    }

    public function testCutIsSafeWhenTheScanFellShortOfTheOffset(): void
    {
        $window = new PageWindow(5, 10);

        self::assertSame(['rows' => [], 'hasMore' => false], $window->cut(['a', 'b']));
    }
}
