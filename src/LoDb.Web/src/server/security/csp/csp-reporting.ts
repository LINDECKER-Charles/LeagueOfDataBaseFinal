// The API's endpoint for the reports (src/LoDb.Api/Hosting/SecurityReports), on the page's own
// origin: a browser sends its Origin, which the API's forgery guard accepts.
const REPORT_PATH = '/api/csp-reports';
// The Reporting API names an endpoint by a group that the policies' `report-to` refers to.
const REPORT_GROUP = 'csp';

/**
 * Where browsers send the violations of a page's policies: `report-to` for those that read
 * the `Reporting-Endpoints` header, `report-uri` for the others, which ignore it otherwise.
 */
export const CSP_REPORTING = {
  /** The directives of a policy that send its violations to the API. */
  directives: [`report-uri ${REPORT_PATH}`, `report-to ${REPORT_GROUP}`],
  /** The `Reporting-Endpoints` header that defines the group, relative to the page. */
  endpoints: `${REPORT_GROUP}="${REPORT_PATH}"`,
} as const;
