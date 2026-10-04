<?php

declare(strict_types=1);

namespace DoctrineMigrations;

use Doctrine\DBAL\Schema\Schema;
use Doctrine\Migrations\AbstractMigration;

/**
 * Preferred profile version: adds users.preferred_version, the Data Dragon patch
 * the owner pins their favorites to so one absent from the browsing version
 * neither disappears from nor gets wiped by the profile. NULL = follow browsing.
 */
final class Version20260719120000 extends AbstractMigration
{
    public function getDescription(): string
    {
        return 'Add users.preferred_version (pinned patch for profile favorites).';
    }

    public function up(Schema $schema): void
    {
        $this->addSql('ALTER TABLE users ADD preferred_version VARCHAR(24) DEFAULT NULL');
    }

    public function down(Schema $schema): void
    {
        $this->addSql('ALTER TABLE users DROP preferred_version');
    }
}
