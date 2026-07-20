import { Location } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';

import { AuthService, ConfirmEmailRequest } from '../../services/auth.service';

const USER_ID_PATTERN = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
const BASE64_URL_PATTERN = /^[A-Za-z0-9_-]{1,4096}$/;

@Component({
  selector: 'app-confirm-email',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink],
  template: `
    <section class="mx-auto max-w-md" aria-labelledby="confirm-email-title">
      <p class="text-caption font-medium uppercase tracking-widest text-accent">Your account</p>
      <h1 id="confirm-email-title" class="mt-2 text-title font-bold text-content-primary">Confirm email</h1>

      @if (state() === 'confirming') {
        <p role="status" aria-live="polite" class="mt-6 rounded-button border border-space-border bg-space-surface px-4 py-3 text-body text-content-primary">
          Confirming your email…
        </p>
      }
      @if (state() === 'success') {
        <div role="status" aria-live="polite" class="mt-6 rounded-button border border-accent/50 bg-accent/10 px-4 py-4 text-body text-content-primary">
          <p class="font-semibold">Your email has been confirmed.</p>
          <a routerLink="/login" class="mt-3 inline-flex font-medium text-accent underline underline-offset-2 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent">Sign in</a>
        </div>
      }
      @if (state() === 'error') {
        <div role="alert" class="mt-6 rounded-button border border-red-400/60 bg-red-400/10 px-4 py-4 text-body text-red-100">
          <p>{{ errorMessage() }}</p>
          <a routerLink="/login" class="mt-3 inline-flex font-medium text-content-primary underline underline-offset-2 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent">Return to sign in</a>
        </div>
      }
    </section>
  `
})
export class ConfirmEmailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly location = inject(Location);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  readonly state = signal<'confirming' | 'success' | 'error'>('confirming');
  readonly errorMessage = signal('');

  ngOnInit(): void {
    const request = createConfirmationRequest(
      this.route.snapshot.queryParamMap.get('userId'),
      this.route.snapshot.queryParamMap.get('code')
    );
    if (request === null) {
      this.state.set('error');
      this.errorMessage.set('This confirmation link is invalid or incomplete. Request a new email and try again.');
      return;
    }

    // Keep the already-validated request only in memory. The confirmation code must not
    // remain in browser history if the following POST returns an error or loses the network.
    this.location.replaceState('/confirm-email');
    this.auth.confirmEmail(request).subscribe({
      next: () => {
        this.state.set('success');
        void this.router.navigate(['/login'], {
          replaceUrl: true,
          state: { confirmationSuccess: true }
        });
      },
      error: () => {
        this.state.set('error');
        this.errorMessage.set('We could not confirm this email link. Request a new email and try again.');
      }
    });
  }
}

function createConfirmationRequest(
  userId: string | null,
  code: string | null
): ConfirmEmailRequest | null {
  if (
    userId === null ||
    code === null ||
    !USER_ID_PATTERN.test(userId) ||
    !BASE64_URL_PATTERN.test(code) ||
    code.length % 4 === 1
  ) {
    return null;
  }

  return { userId, code };
}
