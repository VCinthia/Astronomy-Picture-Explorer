/**
 * Application-owned APOD contract exposed by the P3 backend.
 *
 * The backend keeps the snake_case JSON field names but deliberately omits
 * provider-only metadata. Optional NASA values are normalized to `null`, so
 * rendering code never needs provider-specific presence checks.
 */

export type ApodMediaType = 'image' | 'video';

export interface ApodEntry {
  /** Date of the entry in `YYYY-MM-DD` format. */
  date: string;
  title: string;
  /** Long-form description; also the source for descriptive image `alt` text. */
  explanation: string;
  media_type: ApodMediaType;
  /** Display URL: the image for `image`, or the video page for `video`. */
  url: string;
  /** High-resolution image URL; present for `image` entries only. */
  hdurl: string | null;
  /** Still preview for `video` entries (requested with `thumbs=true`). */
  thumbnail_url: string | null;
  /** Image author/credit, when APOD provides one. */
  copyright: string | null;
}
