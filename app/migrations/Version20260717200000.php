<?php

declare(strict_types=1);

namespace DoctrineMigrations;

use Doctrine\DBAL\Schema\Schema;
use Doctrine\Migrations\AbstractMigration;

final class Version20260717200000 extends AbstractMigration
{
    public function getDescription(): string
    {
        return 'Moderation lot: ban state on users (is_banned flag, banned_at TIMESTAMPTZ, optional ban_reason) — '
            . 'enforced at login (UserChecker) and on the public surfaces (profile 404, trends exclusion).';
    }

    public function up(Schema $schema): void
    {
        $this->addSql('ALTER TABLE users ADD is_banned BOOLEAN DEFAULT false NOT NULL');
        $this->addSql('ALTER TABLE users ADD banned_at TIMESTAMP(0) WITH TIME ZONE DEFAULT NULL');
        $this->addSql('ALTER TABLE users ADD ban_reason VARCHAR(255) DEFAULT NULL');
    }

    public function down(Schema $schema): void
    {
        $this->addSql('ALTER TABLE users DROP is_banned');
        $this->addSql('ALTER TABLE users DROP banned_at');
        $this->addSql('ALTER TABLE users DROP ban_reason');
    }
}
