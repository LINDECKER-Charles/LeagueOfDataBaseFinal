<?php
declare(strict_types=1);

namespace App\Tests\Unit\Service\Mail;

use App\Dto\ContactSubmission;
use App\Entity\ContactMessage;
use App\Entity\Enum\ContactCategory;
use App\Entity\User;
use App\Service\Mail\AuthMailer;
use App\Service\Mail\ContactMailer;
use App\Tests\Unit\Support\RecordingLogger;
use Monolog\Formatter\JsonFormatter;
use Monolog\Level;
use Monolog\LogRecord;
use PHPUnit\Framework\TestCase;
use Psr\Log\LogLevel;
use Symfony\Component\Mailer\Exception\TransportException;
use Symfony\Component\Mailer\MailerInterface;
use Symfony\Component\Routing\Generator\UrlGeneratorInterface;
use Symfony\Contracts\Translation\TranslatorInterface;
use SymfonyCasts\Bundle\ResetPassword\Model\ResetPasswordToken;
use SymfonyCasts\Bundle\VerifyEmail\VerifyEmailHelperInterface;

/**
 * A relay outage used to be swallowed whole: the controller catches it (the
 * message is already persisted for the /admin inbox) and nothing else existed.
 * These tests pin BOTH halves of the fix — the failure is now named, and the
 * exception still reaches the caller so no policy changed.
 */
final class MailSendFailureTest extends TestCase
{
    private const VISITOR_EMAIL = 'visitor@example.com';

    public function testARelayOutageIsReportedAndStillPropagates(): void
    {
        $logger = new RecordingLogger();
        $mailer = new ContactMailer($this->failingMailer(), $logger, 'no-reply@lodb.test', 'ops@lodb.test');

        try {
            $mailer->sendNotification($this->message());
            self::fail('the exception must still reach the caller — its policy is unchanged');
        } catch (TransportException) {
            // expected
        }

        self::assertSame(LogLevel::ERROR, $logger->only('mail.send.failed')['level']);
    }

    /**
     * A log line lands in a 90-day searchable index, outside the retention we
     * declare: the visitor's address must not travel with it.
     *
     * The relay writes the exception message itself, and a rejection echoes the
     * envelope recipient back — so the fixture below carries the address exactly
     * the way a real `550` would, and the assertion runs on what MONOLOG would
     * write. `json_encode()` on the context is not a substitute: a Throwable's
     * message is protected, so `json_encode(new RuntimeException('x@y.z'))`
     * renders `{}` while Monolog's normaliser reads it — an assertion on
     * `json_encode` passes unconditionally and guards nothing.
     */
    public function testTheReportCarriesNoAddress(): void
    {
        $logger = new RecordingLogger();
        $mailer = new ContactMailer(
            $this->rejectingRecipient(),
            $logger,
            'no-reply@lodb.test',
            'ops@lodb.test',
        );

        try {
            $mailer->sendNotification($this->message());
        } catch (TransportException) {
            // asserted above
        }

        $record = $logger->only('mail.send.failed');

        self::assertStringNotContainsString(self::VISITOR_EMAIL, $this->asMonologWouldWrite($record));
        self::assertSame('contact_notification', $record['context']['kind']);
    }

    /** The failure must still be identifiable: only the relay's free text is dropped. */
    public function testTheReportStillIdentifiesTheFailure(): void
    {
        $logger = new RecordingLogger();
        $mailer = new ContactMailer(
            $this->rejectingRecipient(),
            $logger,
            'no-reply@lodb.test',
            'ops@lodb.test',
        );

        try {
            $mailer->sendNotification($this->message());
        } catch (TransportException) {
            // asserted above
        }

        $context = $logger->only('mail.send.failed')['context'];

        self::assertSame(TransportException::class, $context['exception_class']);
        self::assertArrayHasKey('exception_at', $context);
        self::assertArrayNotHasKey('exception', $context, 'the object carries the relay reply');
    }

    /** A disabled recipient is not a failure: nothing is sent and nothing is logged. */
    public function testADisabledRecipientSendsAndLogsNothing(): void
    {
        $logger = new RecordingLogger();
        $mailer = new ContactMailer($this->failingMailer(), $logger, 'no-reply@lodb.test', '');

        $mailer->sendNotification($this->message());

        self::assertSame([], $logger->keys());
    }

    /** A delivery that worked must stay silent. */
    public function testASuccessfulSendLogsNothing(): void
    {
        $logger = new RecordingLogger();
        $silent = $this->createStub(MailerInterface::class);
        $mailer = new ContactMailer($silent, $logger, 'no-reply@lodb.test', 'ops@lodb.test');

        $mailer->sendNotification($this->message());

        self::assertSame([], $logger->keys());
    }

    /**
     * Same guarantee on the account-lifecycle path, which has its OWN delivery
     * method: registration swallows the failure behind a resend banner, the reset
     * flow stays silent so the outcome cannot probe which addresses exist. Both
     * policies survive; the outage stops being invisible.
     */
    public function testAnAccountMailOutageIsReportedWithoutTheAddressAndStillPropagates(): void
    {
        $logger = new RecordingLogger();
        $user = new User();
        $user->setEmail(self::VISITOR_EMAIL);
        $user->setUsername('visitor');
        $mailer = new AuthMailer(
            $this->rejectingRecipient(),
            $this->createStub(VerifyEmailHelperInterface::class),
            $this->createStub(UrlGeneratorInterface::class),
            $this->passthroughTranslator(),
            $logger,
            'no-reply@lodb.test',
        );

        try {
            $mailer->sendPasswordReset($user, new ResetPasswordToken('tok', new \DateTimeImmutable(), 1));
            self::fail('the exception must still reach the caller — its policy is unchanged');
        } catch (TransportException) {
            // expected
        }

        $record = $logger->only('mail.send.failed');

        self::assertSame(LogLevel::ERROR, $record['level']);
        self::assertStringNotContainsString(self::VISITOR_EMAIL, $this->asMonologWouldWrite($record));
    }

    /**
     * The bytes Monolog would actually write for a captured record. Assertions
     * about what a log does NOT contain have to run on this, not on the raw
     * context: the normaliser reads a Throwable's protected message, json_encode
     * cannot, and the two therefore disagree about exactly the field that leaks.
     *
     * @param array{level: string, message: string, context: array<string, mixed>} $record
     */
    private function asMonologWouldWrite(array $record): string
    {
        return (new JsonFormatter())->format(new LogRecord(
            new \DateTimeImmutable(),
            'mail',
            Level::Error,
            $record['message'],
            $record['context'],
        ));
    }

    private function passthroughTranslator(): TranslatorInterface
    {
        $translator = $this->createStub(TranslatorInterface::class);
        $translator->method('trans')->willReturn('Subject');

        return $translator;
    }

    private function failingMailer(): MailerInterface
    {
        $mailer = $this->createStub(MailerInterface::class);
        $mailer->method('send')
            ->willThrowException(new TransportException('smtp.example.com refused the connection'));

        return $mailer;
    }

    /**
     * A relay refusing the envelope recipient. Symfony's SmtpTransport embeds the
     * RAW reply in the exception message, so this is the shape the real thing has.
     */
    private function rejectingRecipient(): MailerInterface
    {
        $mailer = $this->createStub(MailerInterface::class);
        $mailer->method('send')->willThrowException(new TransportException(sprintf(
            'Expected response code "250" but got code "550", with message '
            . '"550 5.1.1 <%s>: Recipient address rejected: User unknown".',
            self::VISITOR_EMAIL,
        )));

        return $mailer;
    }

    private function message(): ContactMessage
    {
        return new ContactMessage(new ContactSubmission(
            category: ContactCategory::Bug,
            email: self::VISITOR_EMAIL,
            message: 'The item list is empty on the latest patch.',
            name: 'Visitor',
            subject: 'Broken page',
        ));
    }
}
