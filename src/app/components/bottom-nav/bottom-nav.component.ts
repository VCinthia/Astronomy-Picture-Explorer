import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';

/** Primary navigation for viewports below Tailwind's md breakpoint. */
@Component({
  selector: 'app-bottom-nav',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, RouterLinkActive],
  template: `
    <nav
      aria-label="Mobile primary"
      class="fixed inset-x-0 bottom-0 z-50 h-14 border-t border-space-border bg-space-surface md:hidden"
    >
      <div class="mx-auto flex h-full max-w-content">
        <a
          routerLink="/home"
          #homeActive="routerLinkActive"
          routerLinkActive=""
          ariaCurrentWhenActive="page"
          [routerLinkActiveOptions]="{ paths: 'exact', queryParams: 'ignored', matrixParams: 'ignored', fragment: 'ignored' }"
          class="flex h-full flex-1 flex-col items-center justify-center gap-1 text-caption font-medium text-content-secondary transition hover:text-content-primary focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
        >
          <svg
            class="size-5"
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            stroke-width="2"
            stroke-linecap="round"
            stroke-linejoin="round"
            aria-hidden="true"
          >
            <path d="m3 10 9-7 9 7" />
            <path d="M5 9v11h14V9" />
            <path d="M9 20v-6h6v6" />
          </svg>
          <span
            [class.border-accent]="homeActive.isActive"
            [class.border-transparent]="!homeActive.isActive"
            class="border-b pb-0.5"
          >
            Home
          </span>
        </a>

        <a
          routerLink="/explorer"
          #exploreActive="routerLinkActive"
          routerLinkActive=""
          ariaCurrentWhenActive="page"
          [routerLinkActiveOptions]="{ paths: 'exact', queryParams: 'ignored', matrixParams: 'ignored', fragment: 'ignored' }"
          class="flex h-full flex-1 flex-col items-center justify-center gap-1 text-caption font-medium text-content-secondary transition hover:text-content-primary focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
        >
          <svg
            class="size-5"
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            stroke-width="2"
            stroke-linecap="round"
            stroke-linejoin="round"
            aria-hidden="true"
          >
            <circle cx="12" cy="12" r="9" />
            <path d="m15.5 8.5-2 5-5 2 2-5 5-2Z" />
          </svg>
          <span
            [class.border-accent]="exploreActive.isActive"
            [class.border-transparent]="!exploreActive.isActive"
            class="border-b pb-0.5"
          >
            Explore
          </span>
        </a>

        <a
          routerLink="/favorites"
          #favoritesActive="routerLinkActive"
          routerLinkActive=""
          ariaCurrentWhenActive="page"
          [routerLinkActiveOptions]="{ paths: 'exact', queryParams: 'ignored', matrixParams: 'ignored', fragment: 'ignored' }"
          class="flex h-full flex-1 flex-col items-center justify-center gap-1 text-caption font-medium text-content-secondary transition hover:text-content-primary focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
        >
          <svg
            class="size-5"
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            stroke-width="2"
            stroke-linecap="round"
            stroke-linejoin="round"
            aria-hidden="true"
          >
            <path
              d="M20.84 4.61a5.5 5.5 0 0 0-7.78 0L12 5.67l-1.06-1.06a5.5 5.5 0 0 0-7.78 7.78L12 21.23l8.84-8.84a5.5 5.5 0 0 0 0-7.78Z"
            />
          </svg>
          <span
            [class.border-accent]="favoritesActive.isActive"
            [class.border-transparent]="!favoritesActive.isActive"
            class="border-b pb-0.5"
          >
            Favorites
          </span>
        </a>
      </div>
    </nav>
  `
})
export class BottomNavComponent {}
