// Usage: node normalize-openapi.mjs <openapi json...>
// XML doc comments of a CRLF checkout reach the OpenAPI descriptions as escaped "\r\n":
// rewrite them as "\n", as a generation on Linux (and the CI) produces them.
import fs from 'node:fs';

const CRLF_ESCAPE = '\\r\\n';
const LF_ESCAPE = '\\n';

for (const file of process.argv.slice(2)) {
  const source = fs.readFileSync(file, 'utf8');
  const count = source.split(CRLF_ESCAPE).length - 1;
  if (count > 0) fs.writeFileSync(file, source.split(CRLF_ESCAPE).join(LF_ESCAPE));
  console.log(`${file}: ${count} escaped CRLF normalized`);
}
