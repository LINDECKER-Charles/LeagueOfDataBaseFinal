<?php
declare(strict_types=1);

namespace App\Tests\Unit\Service\Analytics;

use App\Service\Analytics\GeoLocator;
use PHPUnit\Framework\TestCase;

final class GeoLocatorTest extends TestCase
{
    public function testDegradesGracefullyWithoutDatabase(): void
    {
        $geo = new GeoLocator('', sys_get_temp_dir());

        self::assertFalse($geo->isAvailable());
        self::assertNull($geo->locate('8.8.8.8'));
    }

    public function testPrivateIpIsNeverResolved(): void
    {
        $geo = new GeoLocator('/nonexistent/db.mmdb', sys_get_temp_dir());

        self::assertNull($geo->locate('192.168.1.10'));
        self::assertNull($geo->locate('10.0.0.1'));
        self::assertNull($geo->locate('127.0.0.1'));
    }

    public function testEmptyIpIsNull(): void
    {
        $geo = new GeoLocator('', sys_get_temp_dir());

        self::assertNull($geo->locate(null));
        self::assertNull($geo->locate(''));
    }
}
