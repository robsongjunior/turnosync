import { HttpErrorResponse } from '@angular/common/http';
import { ProblemDetails } from './models';

/** Extracts the ProblemDetails from the error body, if any. In blob responses the body is still a Blob. */
export function problemOf(error: unknown): ProblemDetails | null {
  if (error instanceof HttpErrorResponse && error.error && typeof error.error === 'object') {
    return error.error as ProblemDetails;
  }
  return null;
}

/** Per-field errors (ValidationProblemDetails), keyed in camelCase like the API. */
export function fieldErrorsOf(error: unknown): Record<string, string[]> {
  return problemOf(error)?.errors ?? {};
}

/** A single message to show the user. */
export function describeError(error: unknown): string {
  if (error instanceof HttpErrorResponse && error.status === 0) {
    return 'Cannot reach the server. Check that the API is running.';
  }

  const problem = problemOf(error);
  if (problem?.detail) {
    return problem.detail;
  }

  const messages = Object.values(problem?.errors ?? {}).flat();
  if (messages.length > 0) {
    return messages.join(' ');
  }

  return problem?.title ?? 'Unexpected error. Please try again.';
}
