<?php

declare(strict_types=1);

namespace DoctrineMigrations;

use Doctrine\DBAL\Schema\Schema;
use Doctrine\Migrations\AbstractMigration;

final class Version20260717150000 extends AbstractMigration
{
    public function getDescription(): string
    {
        return 'Game mode on builds (item availability per DDragon map); existing builds default to Summoner\'s Rift.';
    }

    public function up(Schema $schema): void
    {
        $this->addSql("ALTER TABLE builds ADD game_mode VARCHAR(16) DEFAULT 'sr' NOT NULL");
    }

    public function down(Schema $schema): void
    {
        $this->addSql('ALTER TABLE builds DROP game_mode');
    }
}
