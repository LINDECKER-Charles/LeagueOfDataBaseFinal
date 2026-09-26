<?php
declare(strict_types=1);

// Hashes as the legacy stack writes them, through Symfony's NativePasswordHasher and
// SodiumPasswordHasher rules, reproduced here without Symfony. Printed as JSON.

$passwords = require __DIR__.'/cases.php';

// NativePasswordHasher::hash(): bcrypt reads 72 bytes and stops at a NUL, so a longer
// password, or one with a NUL, is hashed with SHA-512 first. crypt() rather than
// password_hash(), which only writes $2y$.
function symfony_bcrypt(string $password, int $cost, string $variant = '2y'): string
{
    if (72 < strlen($password) || str_contains($password, "\0")) {
        $password = base64_encode(hash('sha512', $password, true));
    }
    $salt = substr(strtr(base64_encode(random_bytes(16)), '+', '.'), 0, 22);

    return crypt($password, sprintf('$%s$%02d$%s', $variant, $cost, $salt));
}

$formats = [
    // The 'auto' hasher of security.yaml: bcrypt, cost 13.
    'bcrypt' => static fn (string $p): string => symfony_bcrypt($p, 13),
    // The test environment's lowest work factor.
    'bcrypt-cost4' => static fn (string $p): string => symfony_bcrypt($p, 4),
    // Older libraries write $2a$, which PHP reads as $2y$ for any UTF-8 password.
    'bcrypt-2a' => static fn (string $p): string => symfony_bcrypt($p, 10, '2a'),
    // SodiumPasswordHasher: libsodium, opslimit 4, memlimit 64 MiB.
    'argon2id-sodium' => static fn (string $p): string => sodium_crypto_pwhash_str(
        $p,
        4,
        64 * 1024 * 1024,
    ),
    // NativePasswordHasher configured for argon2id or argon2i: PHP's defaults.
    'argon2id' => static fn (string $p): string => password_hash($p, PASSWORD_ARGON2ID),
    'argon2i' => static fn (string $p): string => password_hash($p, PASSWORD_ARGON2I),
    // The test environment's argon2 (memory_cost 10 KiB, time_cost 3).
    'argon2id-weak' => static fn (string $p): string => password_hash(
        $p,
        PASSWORD_ARGON2ID,
        ['memory_cost' => 10, 'time_cost' => 3, 'threads' => 1],
    ),
    // Two lanes, which libsodium does not read.
    'argon2id-lanes2' => static fn (string $p): string => password_hash(
        $p,
        PASSWORD_ARGON2ID,
        ['memory_cost' => 19456, 'time_cost' => 2, 'threads' => 2],
    ),
];

$cases = [];
foreach ($formats as $format => $hash) {
    foreach ($passwords as $name => $password) {
        $cases[] = [
            'format' => $format,
            'password' => $name,
            'hash' => $hash($password),
        ];
    }
}

echo json_encode(
    [
        'php' => PHP_VERSION,
        'sodium' => SODIUM_LIBRARY_VERSION,
        'passwords' => $passwords,
        'cases' => $cases,
    ],
    JSON_PRETTY_PRINT | JSON_UNESCAPED_SLASHES | JSON_UNESCAPED_UNICODE | JSON_THROW_ON_ERROR,
), "\n";
