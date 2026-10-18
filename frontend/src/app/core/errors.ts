import { HttpErrorResponse } from '@angular/common/http';
import { ProblemDetails } from './models';

/** Extrae el ProblemDetails del cuerpo del error, si lo hay. En respuestas blob el cuerpo sigue siendo un Blob. */
export function problemOf(error: unknown): ProblemDetails | null {
  if (error instanceof HttpErrorResponse && error.error && typeof error.error === 'object') {
    return error.error as ProblemDetails;
  }
  return null;
}

/** Errores por campo (ValidationProblemDetails), con las claves en camelCase como la API. */
export function fieldErrorsOf(error: unknown): Record<string, string[]> {
  return problemOf(error)?.errors ?? {};
}

/** Un único mensaje para mostrar al usuario. */
export function describeError(error: unknown): string {
  if (error instanceof HttpErrorResponse && error.status === 0) {
    return 'No se puede conectar con el servidor. Comprueba que la API esté en ejecución.';
  }

  const problem = problemOf(error);
  if (problem?.detail) {
    return problem.detail;
  }

  const messages = Object.values(problem?.errors ?? {}).flat();
  if (messages.length > 0) {
    return messages.join(' ');
  }

  return problem?.title ?? 'Error inesperado. Inténtalo de nuevo.';
}
