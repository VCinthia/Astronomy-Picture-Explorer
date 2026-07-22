import { readFileSync, writeFileSync } from 'node:fs';
import { resolve } from 'node:path';

const configPath = resolve('netlify.toml');
const placeholder = 'https://render-api-origin.invalid';
const configuredOrigin = process.env.P3_RENDER_API_ORIGIN?.trim();

if (!configuredOrigin) {
  if (process.env.CONTEXT === 'production') {
    throw new Error(
      'P3_RENDER_API_ORIGIN is required for a production Netlify deploy.');
  }

  console.log('P3_RENDER_API_ORIGIN is unset; proxy redirects remain intentionally inert.');
  process.exit(0);
}

let apiOrigin;
try {
  const candidate = new URL(configuredOrigin);
  if (
    candidate.protocol !== 'https:' ||
    candidate.username ||
    candidate.password ||
    candidate.pathname !== '/' ||
    candidate.search ||
    candidate.hash
  ) {
    throw new Error('must be an HTTPS origin without credentials, path, query, or fragment');
  }
  apiOrigin = candidate.origin;
} catch (error) {
  const reason = error instanceof Error ? error.message : 'invalid value';
  throw new Error(`P3_RENDER_API_ORIGIN ${reason}.`);
}

const config = readFileSync(configPath, 'utf8');
if (!config.includes(placeholder)) {
  throw new Error('Netlify redirect placeholder is missing; refusing to alter the config.');
}

writeFileSync(configPath, config.replaceAll(placeholder, apiOrigin), 'utf8');
console.log(`Prepared signed Netlify proxy redirects for ${apiOrigin}.`);
