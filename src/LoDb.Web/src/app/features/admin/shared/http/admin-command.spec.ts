import { type HttpClient, HttpStatusCode } from '@angular/common/http';
import { HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import type { Observable } from 'rxjs';
import type { StrictHttpResponse } from '../../../../core/api/generated/strict-http-response';
import { AdminNotices } from '../../layout/admin-notices';
import { ADMIN_API } from '../../testing/admin-api';
import { configureAdminTestBed } from '../../testing/admin-test-bed';
import { AdminCommand } from './admin-command';
import { ADMIN_TIMEOUT } from './admin-timeout';

interface Ban {
  readonly id: number;
}

const BAN_URL = `${ADMIN_API}/api/admin/users/4/ban`;

// As the generated client calls an operation answering 204: its errors come back as text.
function banUser(http: HttpClient, rootUrl: string, params: Ban) {
  return http.post(`${rootUrl}/api/admin/users/${params.id}/ban`, null, {
    observe: 'response',
    responseType: 'text',
  }) as unknown as Observable<StrictHttpResponse<void>>;
}

function setUp(timeout = 30_000) {
  configureAdminTestBed([], [{ provide: ADMIN_TIMEOUT, useValue: timeout }]);
  return {
    command: TestBed.inject(AdminCommand),
    http: TestBed.inject(HttpTestingController),
    notices: TestBed.inject(AdminNotices),
  };
}

describe('AdminCommand', () => {
  it('waits 30 seconds for the API by default', () => {
    configureAdminTestBed();

    expect(TestBed.inject(ADMIN_TIMEOUT)).toBe(30_000);
  });

  it('tells a success in the band of the page and answers the body', async () => {
    const { command, http, notices } = setUp();

    const run = command.run(banUser, { id: 4 }, { key: 'users.done.ban', params: { name: 'x' } });
    http.expectOne(BAN_URL).flush('');

    await expect(run).resolves.toBe('');
    expect(notices.current()).toEqual({ tone: 'notice', text: 'admin.users.done.ban' });
  });

  it('tells why the API refused, from the code of a problem sent as text', async () => {
    const { command, http, notices } = setUp();

    const run = command.run(banUser, { id: 4 }, { key: 'users.done.ban' });
    http.expectOne(BAN_URL).flush(JSON.stringify({ code: 'self-moderation' }), {
      status: HttpStatusCode.Conflict,
      statusText: 'Conflict',
    });

    await expect(run).resolves.toBeNull();
    expect(notices.current()).toEqual({ tone: 'alert', text: 'admin.errors.self_moderation' });
  });

  it('reads a lost session and an unknown refusal', async () => {
    const { command, http, notices } = setUp();

    const lost = command.run(banUser, { id: 4 }, { key: 'users.done.ban' });
    http.expectOne(BAN_URL).flush('', { status: HttpStatusCode.Forbidden, statusText: 'x' });
    await lost;
    const session = notices.current()?.text;
    const failed = command.run(banUser, { id: 4 }, { key: 'users.done.ban' });
    http.expectOne(BAN_URL).flush('oops', { status: HttpStatusCode.BadGateway, statusText: 'x' });
    await failed;

    expect([session, notices.current()?.text]).toEqual([
      'admin.errors.session',
      'admin.errors.generic',
    ]);
  });

  it('gives up on an API that does not answer', async () => {
    const { command, http, notices } = setUp(10);

    const run = command.run(banUser, { id: 4 }, { key: 'users.done.ban' });
    http.expectOne(BAN_URL);

    await expect(run).resolves.toBeNull();
    expect(notices.current()?.text).toBe('admin.errors.network');
  });
});
