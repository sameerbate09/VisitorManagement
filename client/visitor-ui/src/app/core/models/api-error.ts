import { HttpErrorResponse } from '@angular/common/http';
import { CORRELATION_HEADER } from '../config';
import { ApiError } from './api.models';

const FALLBACK = 'Something went wrong. Please try again.';

export function toApiError(error: unknown): ApiError {
  if (!(error instanceof HttpErrorResponse)) {
    return { status: -1, title: 'Error', detail: FALLBACK, code: 'client_error' };
  }

  if (error.status === 0) {
    return {
      status: 0,
      title: 'Network error',
      detail: 'Unable to reach the server. Please check your connection and try again.',
      code: 'network_error',
    };
  }

  const body = error.error;
  const headerId = error.headers?.get(CORRELATION_HEADER) ?? undefined;

  if (body && typeof body === 'object') {
    return {
      status: error.status,
      title: typeof body.title === 'string' ? body.title : 'Error',
      // The API only ever sends safe, user-friendly messages in `detail`.
      detail: typeof body.detail === 'string' ? body.detail : FALLBACK,
      code: typeof body.code === 'string' ? body.code : 'unknown',
      correlationId: typeof body.correlationId === 'string' ? body.correlationId : headerId,
      errors: body.errors && typeof body.errors === 'object' ? body.errors : undefined,
    };
  }

  return { status: error.status, title: 'Error', detail: FALLBACK, code: 'unknown', correlationId: headerId };
}