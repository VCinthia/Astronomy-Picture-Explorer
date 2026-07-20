/**
 * Allows only a path owned by this SPA; protocol, host and protocol-relative
 * URLs fail closed. Both login and anonymous favorite CTAs use this boundary.
 */
export function normalizeReturnUrl(value: string | null): string | null {
  if (
    value === null ||
    !value.startsWith('/') ||
    value.startsWith('//') ||
    value.includes('\\') ||
    /^\/(?:%2f|%5c)/i.test(value)
  ) {
    return null;
  }

  try {
    const internalOrigin = 'https://astronomy-explorer.invalid';
    const parsed = new URL(value, internalOrigin);
    if (parsed.origin !== internalOrigin || parsed.pathname.startsWith('/auth/')) {
      return null;
    }

    return `${parsed.pathname}${parsed.search}${parsed.hash}`;
  } catch {
    return null;
  }
}
