/**
 * Single owner of "turn an unknown HttpErrorResponse into a user-facing message".
 * Feature stores used to each carry their own copy (auth.store even drifted the
 * fallback text); every store now imports this one instead.
 */
export function extractError(err: unknown): string {
  if (err && typeof err === 'object' && 'error' in err) {
    const httpError = err as { error: { detail?: string; title?: string } };
    if (httpError.error?.detail) return httpError.error.detail;
    if (httpError.error?.title) return httpError.error.title;
  }
  return 'An unexpected error occurred.';
}