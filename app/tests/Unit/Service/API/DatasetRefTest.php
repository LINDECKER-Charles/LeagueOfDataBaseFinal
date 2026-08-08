<?php
declare(strict_types=1);

namespace App\Tests\Unit\Service\API;

use App\Service\API\DatasetRef;
use PHPUnit\Framework\TestCase;

/**
 * DatasetRef carries the (patch version, Data Dragon language) couple the resource
 * managers address every dataset, storage path and cache entry by. Two things are
 * contractual: it adopts the page selection shape verbatim, and it stays immutable
 * so a locale fallback can never mutate the ref its caller still holds.
 */
final class DatasetRefTest extends TestCase
{
    public function testFromSelectionAdoptsThePageSelectionShape(): void
    {
        // Exactly what PageContextResolver::selection() returns.
        $ref = DatasetRef::fromSelection(['version' => '15.1.1', 'lang' => 'fr_FR']);

        self::assertSame('15.1.1', $ref->version);
        self::assertSame('fr_FR', $ref->lang);
    }

    public function testWithLangKeepsThePatchAndLeavesTheOriginalUntouched(): void
    {
        $requested = new DatasetRef('7.21.1', 'fr_FR');
        $fallback  = $requested->withLang('en_US');

        self::assertSame('7.21.1', $fallback->version);
        self::assertSame('en_US', $fallback->lang);
        self::assertSame('fr_FR', $requested->lang, 'the source ref must not be mutated');
    }
}
