import { CSP_REPORTING } from './csp-reporting';
import { documentPolicy } from './document-policy';
import { trustedTypesPolicy } from './trusted-types-policy';

/**
 * The policy headers of a document whose inline scripts `scriptHashes` admits: the enforced
 * Content-Security-Policy, the Trusted Types policy in Report-Only, and the endpoint both
 * report their violations to.
 */
export function documentPolicyHeaders(scriptHashes: readonly string[]): Record<string, string> {
  return {
    'Content-Security-Policy': documentPolicy(scriptHashes),
    'Content-Security-Policy-Report-Only': trustedTypesPolicy(),
    'Reporting-Endpoints': CSP_REPORTING.endpoints,
  };
}
