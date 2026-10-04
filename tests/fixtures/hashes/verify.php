<?php
declare(strict_types=1);

// Checks hashes made elsewhere as the legacy stack would: password_verify(), and
// NativePasswordHasher::verify(), which hands argon2 hashes to libsodium. Reads a JSON array
// of {"password", "hash"} from the file given as argument; prints one JSON result each.

$cases = json_decode(file_get_contents($argv[1]), true, flags: JSON_THROW_ON_ERROR);

// NativePasswordHasher::verify(), sodium branch included.
function symfony_verify(string $hash, string $password): bool
{
    if ('' === $password || 4096 < strlen($password)) {
        return false;
    }
    if (!str_starts_with($hash, '$argon')) {
        $bcryptCannotRead = 72 < strlen($password) || str_contains($password, "\0");
        if (str_starts_with($hash, '$2') && $bcryptCannotRead) {
            $password = base64_encode(hash('sha512', $password, true));
        }

        return password_verify($password, $hash);
    }

    return sodium_crypto_pwhash_str_verify($hash, $password);
}

$results = [];
foreach ($cases as $case) {
    $results[] = [
        'password_verify' => password_verify($case['password'], $case['hash']),
        'symfony' => symfony_verify($case['hash'], $case['password']),
    ];
}

echo json_encode($results, JSON_THROW_ON_ERROR), "\n";
