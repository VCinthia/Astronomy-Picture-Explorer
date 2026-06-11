import {
  ChangeDetectionStrategy,
  Component,
  type ElementRef,
  computed,
  effect,
  input,
  output,
  signal,
  viewChild
} from '@angular/core';

import { cn } from '../../../utils/cn';
import { formatApodDate } from '../../utils/format-date';

/**
 * Collapsible, keyboard-operable picker over the dates that exist in the
 * archive. Collapsed by default (the header date stepper handles quick
 * prev/next), it expands into an ARIA listbox + `aria-activedescendant`: the
 * listbox is the single Tab stop, Arrow/Home/End move the active option and
 * Enter/Space selects it. Only available dates are offered.
 */
@Component({
  selector: 'app-date-picker',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="flex flex-col gap-3">
      <button
        type="button"
        (click)="toggle()"
        [attr.aria-expanded]="expanded()"
        aria-controls="date-picker-listbox"
        class="flex items-center justify-between gap-3 rounded-button border border-space-border bg-space-surface-hi px-4 py-2.5 text-meta text-content-primary transition hover:border-accent focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent sm:min-w-72"
      >
        <span class="flex items-center gap-2">
          <span class="text-micro font-medium uppercase tracking-widest text-content-secondary">
            Pick a date
          </span>
          <span class="font-medium">{{ label(selected()) }}</span>
        </span>
        <svg
          class="size-4 text-content-secondary transition-transform"
          [class.rotate-180]="expanded()"
          viewBox="0 0 24 24"
          fill="none"
          stroke="currentColor"
          stroke-width="2"
          aria-hidden="true"
        >
          <path d="M6 9l6 6 6-6" stroke-linecap="round" stroke-linejoin="round" />
        </svg>
      </button>

      @if (expanded()) {
        <div
          #listbox
          id="date-picker-listbox"
          role="listbox"
          tabindex="0"
          aria-label="Available dates"
          [attr.aria-activedescendant]="activeOptionId()"
          (keydown)="onKeydown($event)"
          class="flex flex-wrap gap-2 rounded-card focus-visible:outline-2 focus-visible:outline-offset-4 focus-visible:outline-accent"
        >
          @for (date of dates(); track date; let i = $index) {
            <div
              [id]="optionId(date)"
              role="option"
              [attr.aria-selected]="date === selected()"
              (click)="select(date)"
              [class]="optionClass(date, i)"
            >
              {{ label(date) }}
            </div>
          }
        </div>
      }
    </div>
  `
})
export class DatePickerComponent {
  /** Available dates (`YYYY-MM-DD`), in display order. */
  readonly dates = input.required<readonly string[]>();
  /** Currently selected date. */
  readonly selected = input.required<string>();

  /** Emits when the user picks a date. */
  readonly dateSelected = output<string>();

  /** Whether the date list is open. Collapsed by default. */
  readonly expanded = signal(false);

  /** Index of the option highlighted by keyboard navigation. */
  readonly activeIndex = signal(0);

  private readonly listbox = viewChild<ElementRef<HTMLElement>>('listbox');

  readonly activeOptionId = computed(() => {
    const date = this.dates()[this.activeIndex()];
    return date ? this.optionId(date) : null;
  });

  constructor() {
    // Keep the active option in sync with the externally selected date.
    effect(() => {
      const index = this.dates().indexOf(this.selected());
      this.activeIndex.set(index < 0 ? 0 : index);
    });
    // Move focus into the list when it opens, for keyboard users.
    effect(() => {
      if (this.expanded()) {
        this.listbox()?.nativeElement.focus();
      }
    });
  }

  toggle(): void {
    this.expanded.update((open) => !open);
  }

  optionId(date: string): string {
    return `date-option-${date}`;
  }

  label(date: string): string {
    return formatApodDate(date);
  }

  optionClass(date: string, index: number): string {
    return cn(
      'cursor-pointer rounded-chip border px-3 py-1.5 text-meta transition',
      date === this.selected()
        ? 'border-accent bg-accent/20 text-accent'
        : 'border-space-border bg-space-surface-hi text-content-secondary hover:border-space-border/80',
      index === this.activeIndex() && 'ring-2 ring-accent'
    );
  }

  select(date: string): void {
    this.dateSelected.emit(date);
    this.expanded.set(false);
  }

  onKeydown(event: KeyboardEvent): void {
    const lastIndex = this.dates().length - 1;
    switch (event.key) {
      case 'ArrowDown':
      case 'ArrowRight':
        this.activeIndex.update((i) => Math.min(i + 1, lastIndex));
        break;
      case 'ArrowUp':
      case 'ArrowLeft':
        this.activeIndex.update((i) => Math.max(i - 1, 0));
        break;
      case 'Home':
        this.activeIndex.set(0);
        break;
      case 'End':
        this.activeIndex.set(lastIndex);
        break;
      case 'Enter':
      case ' ':
        this.select(this.dates()[this.activeIndex()]);
        break;
      case 'Escape':
        this.expanded.set(false);
        break;
      default:
        return;
    }
    event.preventDefault();
  }
}
