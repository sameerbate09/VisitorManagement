import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { API_BASE_URL, CORRELATION_HEADER } from '../config';
import { newCorrelationId } from '../correlation-id';
import { AuthService } from '../services/auth.service';

export const apiInterceptor: HttpInterceptorFn = (req, next) => {
  // Only our own API ever receives the token or the correlation header.
  if (!req.url.startsWith(API_BASE_URL)) return next(req);

  let headers = req.headers.set(CORRELATION_HEADER, newCorrelationId());

  const token = inject(AuthService).getToken();
  if (token) headers = headers.set('Authorization', `Bearer ${token}`);

  return next(req.clone({ headers }));
};