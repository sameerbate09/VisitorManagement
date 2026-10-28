import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';
import { API_BASE_URL, CORRELATION_HEADER } from '../config';
import { AuthService } from '../services/auth.service';
import { ClientLogger } from '../services/client-logger.service';

export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const logger = inject(ClientLogger);

  return next(req).pipe(
    catchError((error: unknown) => {
      if (error instanceof HttpErrorResponse && req.url.startsWith(API_BASE_URL)) {
        const path = new URL(req.url).pathname;
        const correlationId = req.headers.get(CORRELATION_HEADER) ?? undefined;

        // Status and route only. Request and response bodies can contain personal data.
        logger.warn(`${req.method} ${path}`, `request failed with status ${error.status}`, correlationId);

        // A 401 anywhere except the login call means the session is gone.
        if (error.status === 401 && !path.endsWith('/auth/login')) {
          auth.signOut('expired');
        }
      }
      return throwError(() => error);
    }),
  );
};