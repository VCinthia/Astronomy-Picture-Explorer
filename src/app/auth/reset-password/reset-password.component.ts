import { Location } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';

import { AuthService, ResetPasswordRequest } from '../../services/auth.service';

const USER_ID_PATTERN = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
const BASE64_URL_PATTERN = /^[A-Za-z0-9_-]{1,4096}$/;

@Component({
  selector: 'app-reset-password',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, RouterLink],
  template: `
    <section class="mx-auto max-w-md" aria-labelledby="reset-password-title">
      <p class="text-caption font-medium uppercase tracking-widest text-accent">Your account</p>
      <h1 id="reset-password-title" class="mt-2 text-title font-bold text-content-primary">Choose a new password</h1>

      @if (state() === 'invalid' || state() === 'error') {
        <div role="alert" class="mt-6 rounded-button border border-red-400/60 bg-red-400/10 px-4 py-4 text-body text-red-100">
          <p>{{ state() === 'invalid'
            ? 'This password reset link is invalid or incomplete. Request a new reset link and try again.'
            : 'We could not reset this password. Request a new reset link and try again.' }}</p>
          <a routerLink="/forgot-password" class="mt-3 inline-flex font-medium text-content-primary underline underline-offset-2 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent">Request a reset link</a>
        </div>
      } @else {
        <p class="mt-3 text-body text-content-secondary">Use a new password for your account.</p>
        <form class="mt-8 space-y-5" [formGroup]="form" (ngSubmit)="submit()" novalidate>
          <div>
            <label for="reset-password" class="block text-meta font-medium text-content-primary">New password</label>
            <input
              id="reset-password"
              type="password"
              autocomplete="new-password"
              formControlName="password"
              [attr.aria-invalid]="password.invalid && (password.touched || submitted())"
              aria-describedby="reset-password-error"
              class="mt-2 block w-full rounded-button border border-space-border bg-space-surface px-3 py-2.5 text-body text-content-primary outline-none transition focus:border-accent focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
            />
            @if (password.invalid && (password.touched || submitted())) {
              <p id="reset-password-error" class="mt-2 text-meta text-red-300">Enter a password with at least 6 characters.</p>
            }
          </div>

          <div>
            <label for="reset-password-confirm" class="block text-meta font-medium text-content-primary">Confirm new password</label>
            <input
              id="reset-password-confirm"
              type="password"
              autocomplete="new-password"
              formControlName="passwordConfirmation"
              [attr.aria-invalid]="passwordConfirmation.invalid && (passwordConfirmation.touched || submitted())"
              aria-describedby="reset-password-confirm-error"
              class="mt-2 block w-full rounded-button border border-space-border bg-space-surface px-3 py-2.5 text-body text-content-primary outline-none transition focus:border-accent focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
            />
            @if (passwordConfirmation.invalid && (passwordConfirmation.touched || submitted())) {
              <p id="reset-password-confirm-error" class="mt-2 text-meta text-red-300">Passwords must match.</p>
            }
          </div>

          <button
            type="submit"
            [disabled]="auth.loading()"
            class="inline-flex w-full justify-center rounded-button bg-accent px-5 py-3 text-meta font-semibold text-space-base transition hover:opacity-90 disabled:cursor-not-allowed disabled:opacity-60 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
          >
            {{ auth.loading() ? 'Resetting password…' : 'Reset password' }}
          </button>
        </form>
      }
    </section>
  `
})
export class ResetPasswordComponent implements OnInit {
  readonly auth = inject(AuthService);
  private readonly formBuilder = inject(NonNullableFormBuilder);
  private readonly route = inject(ActivatedRoute);
  private readonly location = inject(Location);
  private readonly router = inject(Router);
  private resetLink: Pick<ResetPasswordRequest, 'userId' | 'code'> | null = null;

  readonly state = signal<'ready' | 'invalid' | 'error'>('ready');
  readonly submitted = signal(false);
  readonly form = this.formBuilder.group({
    password: ['', [Validators.required, Validators.minLength(6)]],
    passwordConfirmation: ['', Validators.required]
  });

  get password() {
    return this.form.controls.password;
  }

  get passwordConfirmation() {
    return this.form.controls.passwordConfirmation;
  }

  ngOnInit(): void {
    this.resetLink = createResetLink(
      this.route.snapshot.queryParamMap.get('userId'),
      this.route.snapshot.queryParamMap.get('code')
    );
    if (this.resetLink === null) {
      this.state.set('invalid');
      return;
    }

    // The valid capability is kept only in this component's memory and removed from
    // history before the form can be rendered or a network request can be made.
    this.location.replaceState('/reset-password');
  }

  submit(): void {
    this.submitted.set(true);
    this.state.set('ready');
    if (this.password.value !== this.passwordConfirmation.value) {
      this.passwordConfirmation.setErrors({ mismatch: true });
    } else {
      this.passwordConfirmation.setErrors(null);
    }

    if (this.form.invalid || this.resetLink === null) {
      this.form.markAllAsTouched();
      return;
    }

    const request: ResetPasswordRequest = {
      ...this.resetLink,
      password: this.password.value
    };
    this.resetLink = null;
    this.auth.resetPassword(request).subscribe({
      next: () => {
        void this.router.navigate(['/login'], {
          replaceUrl: true,
          state: { passwordResetSuccess: true }
        });
      },
      error: () => this.state.set('error')
    });
  }
}

function createResetLink(
  userId: string | null,
  code: string | null
): Pick<ResetPasswordRequest, 'userId' | 'code'> | null {
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
