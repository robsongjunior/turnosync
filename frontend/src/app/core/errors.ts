import { HttpErrorResponse } from '@angular/common/http';
import { ProblemDetails } from './models';

/** Extrai o ProblemDetails do corpo do erro, quando houver. Em respostas blob o corpo ainda é um Blob. */
export function problemOf(error: unknown): ProblemDetails | null {
  if (error instanceof HttpErrorResponse && error.error && typeof error.error === 'object') {
    return error.error as ProblemDetails;
  }
  return null;
}

/** Erros por campo (ValidationProblemDetails), com a chave em camelCase como na API. */
export function fieldErrorsOf(error: unknown): Record<string, string[]> {
  return problemOf(error)?.errors ?? {};
}

/** Mensagem única para mostrar ao usuário. */
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
