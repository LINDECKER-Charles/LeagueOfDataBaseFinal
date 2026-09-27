import { execFileSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { expect, type Page } from '@playwright/test';
import { nextStepAt, totpCode, totpStep } from './totp';

/** An administrator the suite created with the CLI, and the key of its authenticator. */
export interface AdminAccount {
  readonly email: string;
  readonly username: string;
  readonly password: string;
  /** The shared key of the authenticator, once enrolled. */
  sharedKey?: string;
  /** The last time step whose code was used: Identity may refuse a code used twice. */
  lastStep?: number;
}

// The integration stack by default; a slot names its own project (docs/guides/dev-next.md).
const PROJECT = process.env['LODB_E2E_COMPOSE_PROJECT'] ?? 'lodb-next';
const ROOT = fileURLToPath(new URL('../../../../', import.meta.url));
const COMPOSE_FILES = ['compose.next.yaml', 'compose.next.override.yaml'];
// The CLI starts a .NET host and reaches the database: seconds, not minutes.
const CLI_TIMEOUT_MS = 120_000;
const RANDOM_LENGTH = 4;
const CREATED = /^Created the administrator account (\S+) </m;
const PASSWORD = /^Password, shown only now: (\S+)$/m;

/** The French copy of the admin (public/i18n/admin/fr.json), which the specs follow. */
export const LOGIN = {
  identifier: "E-mail ou nom d'utilisateur",
  password: 'Mot de passe',
  submit: 'Continuer',
  code: "Code d'authentification",
  verify: 'Vérifier',
} as const;

const ENROLL = {
  code: "Code à 6 chiffres affiché par l'application",
  confirm: 'Activer',
  codes: 'Codes de récupération',
  open: "Ouvrir l'administration",
} as const;

/**
 * Creates an administrator through `admin create`, run in the API container of the stack:
 * the only way to the first administrator. `purpose` (a few letters) names the account.
 */
export function createAdmin(purpose: string): AdminAccount {
  const random = Math.random()
    .toString(36)
    .slice(2, 2 + RANDOM_LENGTH);
  const email = `e2e_${purpose}_${Date.now().toString(36)}${random}@example.com`;
  const output = execFileSync('docker', composeArguments(email), {
    cwd: ROOT,
    encoding: 'utf8',
    timeout: CLI_TIMEOUT_MS,
  });
  const username = CREATED.exec(output)?.[1];
  const password = PASSWORD.exec(output)?.[1];
  if (username === undefined || password === undefined) {
    throw new Error(`admin create did not create ${email}:\n${output}`);
  }
  return { email, username, password };
}

function composeArguments(email: string): string[] {
  const files = COMPOSE_FILES.flatMap((file) => ['-f', file]);
  const command = ['dotnet', 'LoDb.Api.dll', 'admin', 'create', '--email', email];
  return ['compose', '-p', PROJECT, ...files, 'exec', '-T', 'api', ...command];
}

/**
 * The code of the authenticator, never one already used: within the step of the last code,
 * waits for the next step.
 */
export async function freshCode(page: Page, admin: AdminAccount): Promise<string> {
  const key = admin.sharedKey;
  if (key === undefined) {
    throw new Error(`${admin.username} has no authenticator yet`);
  }
  if (admin.lastStep !== undefined && totpStep() <= admin.lastStep) {
    await page.waitForTimeout(nextStepAt(admin.lastStep) - Date.now());
  }
  const step = totpStep();
  admin.lastStep = step;
  return totpCode(key, step);
}

/** Fills the password step of `/admin/login`, the page already open. */
export async function enterPassword(page: Page, admin: AdminAccount): Promise<void> {
  await page.getByLabel(LOGIN.identifier).fill(admin.email);
  await page.getByLabel(LOGIN.password, { exact: true }).fill(admin.password);
  await page.getByRole('button', { name: LOGIN.submit }).click();
}

/**
 * The first sign-in of a new administrator: the password, then the enrolment of an
 * authenticator, whose key the account keeps, and its recovery codes. Lands on the admin.
 */
export async function enrollAdmin(page: Page, admin: AdminAccount): Promise<void> {
  await page.goto('/admin/login');
  await enterPassword(page, admin);
  await expect(page).toHaveURL(/\/admin\/enroll$/);
  const key = page.locator('[data-shared-key]');
  await expect(key).toHaveText(/\w{4}/);
  admin.sharedKey = (await key.innerText()).trim();
  await page.getByLabel(ENROLL.code).fill(await freshCode(page, admin));
  await page.getByRole('button', { name: ENROLL.confirm }).click();
  await expect(page.getByRole('heading', { name: ENROLL.codes })).toBeVisible();
  await page.getByRole('link', { name: ENROLL.open }).click();
  await expect(page).toHaveURL(/\/admin$/);
}

/** Signs an enrolled administrator in at `/admin/login`: the password, then the code. */
export async function signInAdmin(page: Page, admin: AdminAccount): Promise<void> {
  await enterPassword(page, admin);
  await page.getByLabel(LOGIN.code).fill(await freshCode(page, admin));
  await page.getByRole('button', { name: LOGIN.verify }).click();
}
