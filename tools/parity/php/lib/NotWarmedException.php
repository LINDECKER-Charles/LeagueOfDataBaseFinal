<?php
declare(strict_types=1);

namespace LoDbParity;

/** A payload the export needs is not in the legacy storage: reading it would fetch it. */
final class NotWarmedException extends \RuntimeException
{
}
