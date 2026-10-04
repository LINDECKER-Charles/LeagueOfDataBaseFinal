<?php

declare(strict_types=1);

namespace DoctrineMigrations;

use Doctrine\DBAL\Schema\Schema;
use Doctrine\Migrations\AbstractMigration;

/**
 * Favorite skin banner: adds users.favorite_skin_id, holding the DDragon splash
 * filename stem "{championId}_{skinNum}" the profile hero renders from.
 */
final class Version20260717214500 extends AbstractMigration
{
    public function getDescription(): string
    {
        return 'Add users.favorite_skin_id (profile banner skin).';
    }

    public function up(Schema $schema): void
    {
        $this->addSql('ALTER TABLE users ADD favorite_skin_id VARCHAR(64) DEFAULT NULL');
    }

    public function down(Schema $schema): void
    {
        $this->addSql('ALTER TABLE users DROP favorite_skin_id');
    }
}
