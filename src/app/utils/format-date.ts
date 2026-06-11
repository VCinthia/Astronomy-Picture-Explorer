const MONTHS = [
  'Jan',
  'Feb',
  'Mar',
  'Apr',
  'May',
  'Jun',
  'Jul',
  'Aug',
  'Sep',
  'Oct',
  'Nov',
  'Dec'
];

/**
 * Format an APOD `YYYY-MM-DD` date as `Mon DD, YYYY` (e.g. `May 22, 2026`).
 *
 * Parsed component-wise rather than via `new Date(iso)` to avoid the UTC
 * midnight / local-timezone off-by-one-day shift. Returns the input unchanged
 * if it is not a well-formed ISO date.
 */
export function formatApodDate(isoDate: string): string {
  const [year, month, day] = isoDate.split('-').map(Number);
  if (!year || !month || !day || month < 1 || month > 12) {
    return isoDate;
  }
  return `${MONTHS[month - 1]} ${String(day).padStart(2, '0')}, ${year}`;
}
