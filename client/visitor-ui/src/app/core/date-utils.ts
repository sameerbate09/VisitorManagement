/** The API sends UTC dates without a trailing Z; without it the browser reads them as local time. */
export function ensureUtc(value: string | null): string | null {
  if (!value) return value;
  return /(Z|[+-]\d{2}:?\d{2})$/i.test(value) ? value : `${value}Z`;
}

/** Combines a calendar date and an HH:mm time into one local Date. */
export function combineDateTime(date: Date | null, time: string): Date | null {
  if (!date || !/^\d{2}:\d{2}$/.test(time)) return null;
  const [hours, minutes] = time.split(':').map(Number);
  return new Date(date.getFullYear(), date.getMonth(), date.getDate(), hours, minutes, 0, 0);
}

export function toTimeString(date: Date): string {
  return `${String(date.getHours()).padStart(2, '0')}:${String(date.getMinutes()).padStart(2, '0')}`;
}

export function startOfDay(date: Date): Date {
  return new Date(date.getFullYear(), date.getMonth(), date.getDate());
}