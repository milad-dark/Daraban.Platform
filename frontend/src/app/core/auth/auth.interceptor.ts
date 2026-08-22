import { inject } from '@angular/core';
import {
  HttpInterceptorFn,
  HttpRequest,
  HttpHandlerFn,
  HttpErrorResponse,
} from '@angular/common/http';
import { from, throwError } from 'rxjs';
import { catchError, switchMap } from 'rxjs/operators';
import { AuthStore } from './auth.store';
import { Router } from '@angular/router';

const RETRY_HEADER = 'X-Auth-Retry';

export const authInterceptor: HttpInterceptorFn = (
  req: HttpRequest<unknown>,
  next: HttpHandlerFn
) => {
  const authStore = inject(AuthStore);
  const router = inject(Router);

  let outgoing = req.clone({ withCredentials: true });

  const token = authStore.accessToken();
  if (token) {
    outgoing = outgoing.clone({
      setHeaders: { Authorization: `Bearer ${token}` },
    });
  }

  return next(outgoing).pipe(
    catchError((error: unknown) => {
      if (
        error instanceof HttpErrorResponse &&
        error.status === 401 &&
        !req.headers.has(RETRY_HEADER)
      ) {
        return from(authStore.refresh()).pipe(
          switchMap((refreshed: boolean) => {
            if (!refreshed) {
              router.navigate(['/auth/login']);
              return throwError(() => new Error('Session expired'));
            }
            const newToken = authStore.accessToken();
            const retryReq = req.clone({
              withCredentials: true,
              setHeaders: {
                Authorization: `Bearer ${newToken ?? ''}`,
                [RETRY_HEADER]: '1',
              },
            });
            return next(retryReq);
          })
        );
      }
      return throwError(() => error);
    })
  );
};