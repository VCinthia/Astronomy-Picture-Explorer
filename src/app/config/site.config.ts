/**
 * Centralized site-wide constants (URLs, author info, dates).
 *
 * Keeping these in one place avoids magic strings scattered across components
 * and makes them trivial to update or swap (e.g. the image proxy in P3).
 */

/** Author / portfolio owner. */
export const AUTHOR_NAME = 'Cinthia Vota';
export const AUTHOR_SITE_URL = 'https://cinthiavota.com.ar/';

/** Month/year the site was created (shown in the footer). */
export const SITE_CREATED = 'June 2026';

/**
 * Origin of the CORS-friendly image proxy used only for Canvas palette
 * sampling (NASA's APOD host does not send CORS headers). See cors-proxy.ts.
 */
export const IMAGE_PROXY_ORIGIN = 'https://images.weserv.nl';
