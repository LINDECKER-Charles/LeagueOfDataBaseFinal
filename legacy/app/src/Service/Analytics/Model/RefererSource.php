<?php
declare(strict_types=1);

namespace App\Service\Analytics\Model;

/**
 * Traffic source an inbound Referer is attributed to. The backing values are
 * persisted verbatim in the NDJSON event line (`refSource`) and used as report
 * bucket keys — keep them stable.
 */
enum RefererSource: string
{
    case Direct = 'direct';
    case Internal = 'internal';
    case Search = 'search';
    case Social = 'social';
    case External = 'external';
}
