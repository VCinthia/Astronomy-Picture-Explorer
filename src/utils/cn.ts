import { type ClassValue, clsx } from 'clsx';
import { twMerge } from 'tailwind-merge';

/**
 * Merge conditional class lists into a single deduplicated className.
 *
 * `clsx` resolves conditionals/arrays/objects; `twMerge` then collapses
 * conflicting Tailwind utilities so the last one wins (e.g. `px-2 px-page`).
 */
export function cn(...inputs: ClassValue[]): string {
  return twMerge(clsx(inputs));
}
