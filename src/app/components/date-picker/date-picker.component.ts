import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  input,
  output,
  signal
} from '@angular/core';

import { cn } from '../../../utils/cn';
import { formatApodDate } from '../../utils/format-date';

/**
 * Keyboard-operable picker over the dates that actually exist in the archive
 * (ARIA listbox + `aria-activedescendant`). The listbox is the single Tab stop;
 * Arrow/Home/End move the active option and Enter/Space selects it. Only
 * available dates are offered, so there is no disabled/empty state to navigate.
 */
@Component({
  selector: 'app-date-picker',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="flex flex-col gap-3">
      <span id="date-picker-label" class="text-micro font-medium uppercase tracking-widest text-content-secondary">
        Pick a date
      </span>
      <div
        role="listbox"
        tabindex="0"
        aria-labelledby="date-picker-label"
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

  /** Index of the option highlighted by keyboard navigation. */
  readonly activeIndex = signal(0);

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
      default:
        return;
    }
    event.preventDefault();
  }
}
