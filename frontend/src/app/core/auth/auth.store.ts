import { inject, computed } from '@angular/core';
import {
  signalStore,
  withState,
  withMethods,
  withComputed,
  patchState,
} from '@ngrx/signals';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { AuthService } from './auth.service';
import { CurrentUser } from './models/current-user.model';
import { LoginRequest } from './models/login-request.model';
import { RegisterRequest } from './models/register-request.model';

interface AuthState {
  currentUser: CurrentUser | null;
  accessToken: string | null;
  accessTokenExpiresAt: number | null;
  isLoading: boolean;
  error: string | null;
}

const initialState: AuthState = {
  currentUser: null,
  accessToken: null,
  accessTokenExpiresAt: null,
  isLoading: false,
  error: null,
};

function decodeJwtPayload(token: string): Record<string, unknown> {
  try {
    const base64Payload = token.split('.')[1];
    const decoded = atob(base64Payload.replace(/-/g, '+').replace(/_/g, '/'));
    return JSON.parse(decoded) as Record<string, unknown>;
  } catch {
    return {};
  }
}

function buildCurrentUser(token: string): CurrentUser {
  const payload = decodeJwtPayload(token);
  return {
    id: payload['sub'] as string,
    email: payload['email'] as string,
    firstName: (payload['given_name'] as string) ?? '',
    lastName: (payload['family_name'] as string) ?? '',
    activeEntityId: payload['active_entity_id'] as string,
    roles: (payload['roles'] as string[]) ?? [],
    tokenVersion: (payload['token_version'] as number) ?? 0,
  };
}

function extractErrorMessage(err: unknown): string {
  if (err && typeof err === 'object' && 'error' in err) {
    const httpError = err as { error: { detail?: string; title?: string } };
    if (httpError.error?.detail) return httpError.error.detail;
    if (httpError.error?.title) return httpError.error.title;
  }
  return 'An unexpected error occurred. Please try again.';
}

export const AuthStore = signalStore(
  { providedIn: 'root' },

  withState<AuthState>(initialState),

  withComputed((store) => ({
    isAuthenticated: computed(() => store.accessToken() !== null),
    fullName: computed(() => {
      const user = store.currentUser();
      return user ? `${user.firstName} ${user.lastName}`.trim() : '';
    }),
    isTokenExpiringSoon: computed(() => {
      const expiresAt = store.accessTokenExpiresAt();
      if (!expiresAt) return false;
      return Date.now() >= expiresAt - 60_000;
    }),
  })),

  withMethods(
    (
      store,
      authService = inject(AuthService),
      router = inject(Router)
    ) => ({

      async login(request: LoginRequest): Promise<void> {
        patchState(store, { isLoading: true, error: null });
        try {
          const response = await firstValueFrom(authService.login(request));
          const user = buildCurrentUser(response.accessToken);
          const expiresAt = Date.now() + response.expiresIn * 1000;
          patchState(store, {
            accessToken: response.accessToken,
            accessTokenExpiresAt: expiresAt,
            currentUser: user,
            isLoading: false,
            error: null,
          });
          await router.navigate(['/dashboard']);
        } catch (err: unknown) {
          patchState(store, { isLoading: false, error: extractErrorMessage(err) });
        }
      },

      async register(request: RegisterRequest): Promise<void> {
        patchState(store, { isLoading: true, error: null });
        try {
          const response = await firstValueFrom(authService.register(request));
          const user = buildCurrentUser(response.accessToken);
          const expiresAt = Date.now() + response.expiresIn * 1000;
          patchState(store, {
            accessToken: response.accessToken,
            accessTokenExpiresAt: expiresAt,
            currentUser: user,
            isLoading: false,
            error: null,
          });
          await router.navigate(['/dashboard']);
        } catch (err: unknown) {
          patchState(store, { isLoading: false, error: extractErrorMessage(err) });
        }
      },

      async refresh(): Promise<boolean> {
        try {
          const response = await firstValueFrom(authService.refresh());
          const user = buildCurrentUser(response.accessToken);
          const expiresAt = Date.now() + response.expiresIn * 1000;
          patchState(store, {
            accessToken: response.accessToken,
            accessTokenExpiresAt: expiresAt,
            currentUser: user,
            error: null,
          });
          return true;
        } catch {
          patchState(store, initialState);
          return false;
        }
      },

      async logout(): Promise<void> {
        try {
          await firstValueFrom(authService.logout());
        } finally {
          patchState(store, initialState);
          await router.navigate(['/auth/login']);
        }
      },

      clearError(): void {
        patchState(store, { error: null });
      },
    })
  )
);