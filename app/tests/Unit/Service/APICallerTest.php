<?php

declare(strict_types=1);

namespace App\Tests\Unit\Service;

use App\Service\Tools\APICaller;
use PHPUnit\Framework\TestCase;
use Symfony\Component\HttpClient\Exception\TransportException;
use Symfony\Component\HttpClient\MockHttpClient;
use Symfony\Component\HttpClient\Response\MockResponse;

final class APICallerTest extends TestCase
{
    public function test_call_success_returns_raw_body(): void
    {
        $response = new MockResponse('{"ok":true}', [
            'http_code' => 200,
            'response_headers' => ['content-type: application/json'],
        ]);
        $http = new MockHttpClient($response);

        $svc = new APICaller($http);
        $body = $svc->call('https://example.test/api');

        $this->assertSame('{"ok":true}', $body);
    }

    public function test_call_http_error_status_is_wrapped_into_runtimeexception(): void
    {
        $response = new MockResponse('Boom', ['http_code' => 500]);
        $http = new MockHttpClient($response);

        $svc = new APICaller($http);

        $this->expectException(\RuntimeException::class);
        $this->expectExceptionMessage('APICaller: échec');
        $svc->call('https://example.test/boom');
    }

    public function test_call_transport_error_is_wrapped_into_runtimeexception(): void
    {
        // Simule une panne réseau au moment de request()
        $http = new MockHttpClient(static function () {
            throw new TransportException('network down');
        });

        $svc = new APICaller($http);

        $this->expectException(\RuntimeException::class);
        $this->expectExceptionMessage('APICaller: échec');
        $svc->call('https://example.test/unreachable');
    }
}
