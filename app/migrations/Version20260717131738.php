<?php

declare(strict_types=1);

namespace DoctrineMigrations;

use Doctrine\DBAL\Schema\Schema;
use Doctrine\Migrations\AbstractMigration;

final class Version20260717131738 extends AbstractMigration
{
    public function getDescription(): string
    {
        return 'Google OAuth (nullable password + unique google_id) and the Riot ID tagline on users.';
    }

    public function up(Schema $schema): void
    {
        // google_id is an opaque OpenID `sub`: a plain (case-sensitive) unique
        // index is correct here — the LOWER() convention only covers email/username.
        $this->addSql('ALTER TABLE users ADD google_id VARCHAR(30) DEFAULT NULL');
        $this->addSql('ALTER TABLE users ADD riot_tagline VARCHAR(5) DEFAULT NULL');
        $this->addSql('ALTER TABLE users ALTER password DROP NOT NULL');
        $this->addSql('CREATE UNIQUE INDEX UNIQ_1483A5E976F5C865 ON users (google_id)');
    }

    public function down(Schema $schema): void
    {
        // Reverting NOT NULL fails if OAuth-only accounts (password IS NULL) exist — intended.
        $this->addSql('DROP INDEX UNIQ_1483A5E976F5C865');
        $this->addSql('ALTER TABLE users DROP google_id');
        $this->addSql('ALTER TABLE users DROP riot_tagline');
        $this->addSql('ALTER TABLE users ALTER password SET NOT NULL');
    }
}
