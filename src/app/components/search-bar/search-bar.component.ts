import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  effect,
  inject,
  input,
  output,
  signal
} from '@angular/core';

const SEARCH_DEBOUNCE_MS = 300;
let nextSearchBarId = 0;

/** Reusable keyword input that keeps typing local until its debounce elapses. */
@Component({
  selector: 'app-search-bar',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <label [attr.for]="inputId" class="sr-only">Search astronomy pictures</label>
    <div
      class="relative flex h-11 w-full items-center rounded-button border border-transparent bg-space-surface transition focus-within:border-accent"
    >
      <svg
        class="pointer-events-none absolute left-4 size-4 text-content-secondary"
        viewBox="0 0 24 24"
        fill="none"
        stroke="currentColor"
        stroke-width="2"
        stroke-linecap="round"
        stroke-linejoin="round"
        aria-hidden="true"
      >
        <circle cx="11" cy="11" r="7" />
        <path d="m20 20-4-4" />
      </svg>

      <input
        [id]="inputId"
        type="search"
        inputmode="search"
        autocomplete="off"
        [value]="draft()"
        (input)="onInput($event)"
        placeholder="Search by title or description..."
        class="h-full w-full appearance-none bg-transparent pl-12 pr-10 text-body text-content-primary outline-none placeholder:text-content-secondary"
      />

      @if (draft()) {
        <button
          type="button"
          (click)="clear()"
          aria-label="Clear search"
          class="absolute right-2 flex size-7 items-center justify-center rounded-full bg-space-surface-hi text-content-secondary transition hover:text-content-primary focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
        >
          <svg
            class="size-4"
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            stroke-width="2"
            stroke-linecap="round"
            aria-hidden="true"
          >
            <path d="M6 6l12 12M18 6 6 18" />
          </svg>
        </button>
      }
    </div>
  `
})
export class SearchBarComponent {
  private readonly destroyRef = inject(DestroyRef);
  private debounceTimer: ReturnType<typeof setTimeout> | undefined;

  /** Stable, per-instance relationship between the input and its visible-to-AT label. */
  readonly inputId = `picture-search-${nextSearchBarId++}`;

  /** Current committed query, owned by the parent. */
  readonly query = input.required<string>();
  /** Emits typing after 300 ms, or an empty query immediately when cleared. */
  readonly queryChange = output<string>();

  /** Local input value, so keystrokes do not immediately update shared state. */
  readonly draft = signal('');

  constructor() {
    effect(() => {
      const query = this.query();
      this.cancelPendingEmission();
      this.draft.set(query);
    });

    this.destroyRef.onDestroy(() => this.cancelPendingEmission());
  }

  onInput(event: Event): void {
    const value = (event.target as HTMLInputElement).value;
    this.draft.set(value);
    this.scheduleEmission(value);
  }

  clear(): void {
    this.cancelPendingEmission();
    this.draft.set('');
    this.queryChange.emit('');
  }

  private scheduleEmission(value: string): void {
    this.cancelPendingEmission();
    this.debounceTimer = setTimeout(() => {
      this.debounceTimer = undefined;
      this.queryChange.emit(value);
    }, SEARCH_DEBOUNCE_MS);
  }

  private cancelPendingEmission(): void {
    if (this.debounceTimer !== undefined) {
      clearTimeout(this.debounceTimer);
      this.debounceTimer = undefined;
    }
  }
}
