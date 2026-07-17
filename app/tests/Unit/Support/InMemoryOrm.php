<?php
declare(strict_types=1);

namespace App\Tests\Unit\Support;

use Doctrine\DBAL\DriverManager;
use Doctrine\ORM\EntityManager;
use Doctrine\ORM\ORMSetup;
use Doctrine\ORM\Tools\SchemaTool;

/**
 * Real EntityManager over an in-memory SQLite database, so repository queries
 * (aggregates, joins, unique constraints) are exercised against actual SQL in
 * unit scope — no mocks, no running Postgres. Each call returns a fresh,
 * isolated database.
 */
final class InMemoryOrm
{
    private function __construct()
    {
        // Static factory — never instantiated.
    }

    /** @param list<class-string> $entities Entities whose tables the test needs */
    public static function entityManager(array $entities): EntityManager
    {
        $config = ORMSetup::createAttributeMetadataConfiguration(
            [\dirname(__DIR__, 3) . '/src/Entity'],
            isDevMode: true,
        );
        $connection = DriverManager::getConnection(['driver' => 'pdo_sqlite', 'memory' => true], $config);
        $entityManager = new EntityManager($connection, $config);

        new SchemaTool($entityManager)->createSchema(
            array_map($entityManager->getClassMetadata(...), $entities),
        );

        return $entityManager;
    }
}
