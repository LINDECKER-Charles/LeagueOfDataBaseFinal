<?php
declare(strict_types=1);

namespace App\Tests\Unit\Security;

use App\Entity\User;
use App\Security\UserChecker;
use PHPUnit\Framework\TestCase;
use Symfony\Component\Security\Core\Exception\CustomUserMessageAccountStatusException;
use Symfony\Component\Security\Core\User\InMemoryUser;
use Symfony\Contracts\Translation\TranslatorInterface;

final class UserCheckerTest extends TestCase
{
    private const TRANSLATED = 'Ce compte a été suspendu par l\'équipe de modération.';

    public function testBannedUserIsRejectedWithTheTranslatedMessage(): void
    {
        $user = $this->user();
        $user->ban('spam');

        try {
            $this->checker()->checkPreAuth($user);
            self::fail('A banned user must not pass checkPreAuth.');
        } catch (CustomUserMessageAccountStatusException $e) {
            // The already-translated sentence IS the message key: the login
            // template's |trans over the security domain passes it through.
            self::assertSame(self::TRANSLATED, $e->getMessageKey());
        }
    }

    public function testActiveUserPasses(): void
    {
        $this->checker()->checkPreAuth($this->user());

        $this->addToAssertionCount(1); // no exception = pass
    }

    public function testUnbannedUserPassesAgain(): void
    {
        $user = $this->user();
        $user->ban();
        $user->unban();

        $this->checker()->checkPreAuth($user);

        self::assertNull($user->getBannedAt());
        self::assertNull($user->getBanReason());
    }

    public function testForeignUserTypesAreIgnored(): void
    {
        // The admin firewall's InMemoryUser must never hit the ban logic.
        $this->checker()->checkPreAuth(new InMemoryUser('admin', null, ['ROLE_ADMIN']));

        $this->addToAssertionCount(1);
    }

    private function checker(): UserChecker
    {
        $translator = $this->createStub(TranslatorInterface::class);
        $translator->method('trans')->willReturnCallback(
            static fn (string $id): string => $id === 'auth.flash.banned' ? self::TRANSLATED : $id,
        );

        return new UserChecker($translator);
    }

    private function user(): User
    {
        return new User()->setEmail('probe@example.test')->setUsername('probe');
    }
}
