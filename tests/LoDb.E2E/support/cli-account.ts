import { randomBytes } from 'node:crypto';
import { execInStack } from './stack';

/** An administrator the suite created with the CLI, as `admin create` printed it. */
export interface CreatedAdmin {
  readonly email: string;
  readonly username: string;
  readonly password: string;
  /** The invitation `admin create` printed, which alone opens the enrolment. */
  readonly invitation: string;
}

// Four hexadecimal digits: two runs, or two workers, never share an address.
const RANDOM_BYTES = 2;
const CREATED = /^Created the administrator account (\S+) </m;
const PASSWORD = /^Password, shown only now: (\S+)$/m;
const INVITATION = /^Invitation to enrol an authenticator, valid \d+ minutes: (\S+)$/m;

/**
 * Creates an administrator through `admin create`, run in the API container of the stack:
 * the only way to the first administrator. `purpose` (a few letters) names the account.
 */
export function createAdmin(purpose: string): CreatedAdmin {
  const random = randomBytes(RANDOM_BYTES).toString('hex');
  const email = `e2e_${purpose}_${Date.now().toString(36)}${random}@example.com`;
  const command = ['dotnet', 'LoDb.Api.dll', 'admin', 'create', '--email', email];
  const output = execInStack('api', command);
  const username = CREATED.exec(output)?.[1];
  const password = PASSWORD.exec(output)?.[1];
  const invitation = INVITATION.exec(output)?.[1];
  if (username === undefined || password === undefined || invitation === undefined) {
    throw new Error(`admin create did not create ${email}:\n${output}`);
  }
  return { email, username, password, invitation };
}
