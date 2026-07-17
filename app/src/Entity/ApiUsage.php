<?php
declare(strict_types=1);

namespace App\Entity;

use Doctrine\DBAL\Types\Types;
use Doctrine\ORM\Mapping as ORM;

/**
 * One day of metered requests for one API key. Rows are WRITTEN by the go-api
 * metering flush (upsert ON CONFLICT (api_key_id, day) — the unique constraint
 * below is load-bearing); the PHP side only reads them for the portal and the
 * monthly quota maths. Column names are part of the go-api contract.
 */
#[ORM\Entity]
#[ORM\Table(name: 'api_usage')]
#[ORM\UniqueConstraint(name: 'uniq_api_usage_key_day', columns: ['api_key_id', 'day'])]
final class ApiUsage
{
    #[ORM\Id]
    #[ORM\GeneratedValue]
    #[ORM\Column(type: Types::BIGINT)]
    private ?int $id = null;

    #[ORM\ManyToOne(targetEntity: ApiKey::class)]
    #[ORM\JoinColumn(name: 'api_key_id', nullable: false, onDelete: 'CASCADE')]
    private ApiKey $apiKey;

    #[ORM\Column(type: Types::DATE_IMMUTABLE)]
    private \DateTimeImmutable $day;

    #[ORM\Column(type: Types::BIGINT, options: ['default' => 0])]
    private int $requests = 0;

    public function getId(): ?int
    {
        return $this->id;
    }

    public function getApiKey(): ApiKey
    {
        return $this->apiKey;
    }

    public function getDay(): \DateTimeImmutable
    {
        return $this->day;
    }

    public function getRequests(): int
    {
        return $this->requests;
    }
}
