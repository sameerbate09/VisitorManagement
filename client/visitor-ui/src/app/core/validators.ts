import { AbstractControl, ValidationErrors } from '@angular/forms';

/** Mobile number must contain exactly 10 digits. */
export function mobileValidator(control: AbstractControl): ValidationErrors | null {
  const value = String(control.value ?? '').trim();
  if (!value) return null;   // "required" handles empty
  return /^\d{10}$/.test(value) ? null : { mobile: true };
}

/** The host box must hold a host picked from the list, not free text. */
export function hostSelectedValidator(control: AbstractControl): ValidationErrors | null {
  const value = control.value;
  if (value === null || value === '') return null;   // "required" handles empty
  return typeof value === 'object' ? null : { hostNotSelected: true };
}