/**
 * Data contract for NASA's Astronomy Picture of the Day (APOD) API.
 *
 * Field names intentionally use the snake_case shape returned by the real
 * NASA APOD endpoint (https://github.com/nasa/apod-api) so that swapping the
 * P1 mock for the live backend in P3 is a clean drop-in with no contract change.
 */

export type ApodMediaType = 'image' | 'video';

export interface ApodEntry {
  /** Date of the entry in `YYYY-MM-DD` format; also the mock lookup key. */
  date: string;
  title: string;
  /** Long-form description; also the source for descriptive image `alt` text. */
  explanation: string;
  media_type: ApodMediaType;
  /** APOD API version string, e.g. `"v1"`. */
  service_version: string;
  /** Display URL: the image for `image`, or the video page for `video`. */
  url: string;
  /** High-resolution image URL; present for `image` entries only. */
  hdurl?: string;
  /** Still preview for `video` entries (requested with `thumbs=true`). */
  thumbnail_url?: string;
  /** Image author/credit, when APOD provides one. */
  copyright?: string;
}

/** APOD archive indexed by `date` (`YYYY-MM-DD`) for O(1) lookups. */
export type ApodMock = Record<string, ApodEntry>;
