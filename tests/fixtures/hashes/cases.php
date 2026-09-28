<?php
declare(strict_types=1);

// Passwords shared by generate.php and the .NET tests: ASCII, accented, 4-byte characters,
// past bcrypt's 72 bytes, with a NUL, and at Symfony's 4096-byte limit.
return [
    'ascii' => 'Corr3ct-horse-Battery!',
    'accents' => 'Épée légère — 2026',
    'emoji' => str_repeat('💎', 12),
    'long' => str_repeat('Aa1!', 25),
    'nul' => "Before\0After-2026",
    'max' => str_repeat('Zz9?', 1024),
];
