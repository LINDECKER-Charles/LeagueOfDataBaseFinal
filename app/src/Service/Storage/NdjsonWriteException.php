<?php
declare(strict_types=1);

namespace App\Service\Storage;

/**
 * A row could not be appended to its day file.
 *
 * Exists so {@see NdjsonDayStore::appendRow()} can report a failed write instead
 * of returning as if it had succeeded: the analytics store swallows it (it runs
 * past the flushed response), the audit journal lets
 * {@see \App\Service\Audit\AuditLogger} arbitrate — and a legally retained trail
 * that silently fails to be written is the one outcome nobody may discover
 * months later.
 */
final class NdjsonWriteException extends \RuntimeException
{
}
