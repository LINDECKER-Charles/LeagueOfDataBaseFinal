<?php
declare(strict_types=1);

namespace App\Tests\Unit\Service\Audit;

use App\Service\Audit\AuditAction;
use App\Service\Audit\AuditCategory;
use PHPUnit\Framework\TestCase;

final class AuditActionTest extends TestCase
{
    /**
     * Guards the exhaustiveness of the label() match (no default arm): a new case
     * added without a label throws UnhandledMatchError here rather than in prod.
     */
    public function testEveryActionHasCategoryAndLabel(): void
    {
        foreach (AuditAction::cases() as $action) {
            self::assertInstanceOf(AuditCategory::class, $action->category());
            self::assertNotSame('', $action->label());
        }
    }

    public function testCategoryDerivation(): void
    {
        self::assertSame(AuditCategory::Auth, AuditAction::UserLogin->category());
        self::assertSame(AuditCategory::Account, AuditAction::ProfileUpdate->category());
        self::assertSame(AuditCategory::Build, AuditAction::BuildVote->category());
        self::assertSame(AuditCategory::ApiKey, AuditAction::ApiKeyRevoke->category());
        self::assertSame(AuditCategory::Admin, AuditAction::AdminLogsPurge->category());
    }
}
