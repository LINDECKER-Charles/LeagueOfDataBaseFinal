<?php
declare(strict_types=1);

namespace App\Dto;

use Symfony\Component\DependencyInjection\Attribute\Autowire;

/**
 * Site-identity facts rendered verbatim on the /legal/* pages. Values come from
 * config/packages/legal_info.yaml; the "[[À COMPLÉTER : …]]" placeholders must
 * be replaced before production (tracked in docs/legal-info.md).
 */
final readonly class LegalInfo
{
    public function __construct(
        #[Autowire('%legal.site_name%')]
        public string $siteName,
        #[Autowire('%legal.site_url%')]
        public string $siteUrl,
        #[Autowire('%legal.publisher_name%')]
        public string $publisherName,
        #[Autowire('%legal.publisher_status%')]
        public string $publisherStatus,
        #[Autowire('%legal.publisher_address%')]
        public string $publisherAddress,
        #[Autowire('%legal.publisher_email%')]
        public string $publisherEmail,
        #[Autowire('%legal.publication_director%')]
        public string $publicationDirector,
        #[Autowire('%legal.host_name%')]
        public string $hostName,
        #[Autowire('%legal.host_address%')]
        public string $hostAddress,
        #[Autowire('%legal.host_phone%')]
        public string $hostPhone,
        #[Autowire('%legal.siret%')]
        public string $siret,
        #[Autowire('%legal.dpo_email%')]
        public string $dpoEmail,
        #[Autowire('%legal.jurisdiction_country%')]
        public string $jurisdictionCountry,
        #[Autowire('%legal.effective_date%')]
        public string $effectiveDate,
    ) {}
}
