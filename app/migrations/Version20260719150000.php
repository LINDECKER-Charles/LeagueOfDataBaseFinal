<?php

declare(strict_types=1);

namespace DoctrineMigrations;

use Doctrine\DBAL\Schema\Schema;
use Doctrine\Migrations\AbstractMigration;

/**
 * Builds gain an authoring-language column: the Data Dragon locale the author
 * wrote the name/description in, powering the trends language filter. The
 * NOT NULL DEFAULT 'en_US' backfills every pre-existing build in one shot
 * (Postgres fills existing rows from the column default).
 */
final class Version20260719150000 extends AbstractMigration
{
    public function getDescription(): string
    {
        return "Add builds.language (authoring locale, default 'en_US' backfills legacy builds).";
    }

    public function up(Schema $schema): void
    {
        $this->addSql("ALTER TABLE builds ADD language VARCHAR(8) DEFAULT 'en_US' NOT NULL");
    }

    public function down(Schema $schema): void
    {
        $this->addSql('ALTER TABLE builds DROP language');
    }
}
