/** The API sends UTC dates without a trailing Z; without it the browser reads them as local time. */
export function ensureUtc(value: string | null): string | null {
  if (!value) return value;
  return /(Z|[+-]\d{2}:?\d{2})$/i.test(value) ? value : `${value}Z`;
}