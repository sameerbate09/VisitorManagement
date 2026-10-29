import { Component, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { AbstractControl, FormControl, FormGroup, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { MatAutocompleteModule } from '@angular/material/autocomplete';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DATE_LOCALE, provideNativeDateAdapter } from '@angular/material/core';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { catchError, debounceTime, distinctUntilChanged, filter, map, of, startWith, switchMap, tap } from 'rxjs';
import { combineDateTime, startOfDay, toTimeString } from '../../core/date-utils';
import { toApiError } from '../../core/models/api-error';
import { Host, Purpose, Visitor, VisitorRequest, VisitorStatus } from '../../core/models/api.models';
import { VisitorApiService } from '../../core/services/visitor-api.service';
import { hostSelectedValidator, mobileValidator } from '../../core/validators';

export interface VisitorFormDialogData {
  visitor: Visitor | null;
}

const PAST_GRACE_MS = 15 * 60_000;
const MAX_FUTURE_DAYS = 365;

@Component({
  selector: 'app-visitor-form-dialog',
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatAutocompleteModule,
    MatDatepickerModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatSnackBarModule,
  ],
  providers: [provideNativeDateAdapter(), { provide: MAT_DATE_LOCALE, useValue: 'en-GB' }],
  templateUrl: './visitor-form-dialog.component.html',
  styleUrl: './visitor-form-dialog.component.scss',
})
export class VisitorFormDialogComponent {
  private readonly api = inject(VisitorApiService);
  private readonly snackBar = inject(MatSnackBar);
  private readonly dialogRef = inject(MatDialogRef<VisitorFormDialogComponent, Visitor>);
  private readonly data = inject<VisitorFormDialogData>(MAT_DIALOG_DATA);

  private visitor: Visitor | null = this.data.visitor;
  readonly isEdit = this.visitor !== null;

  readonly statuses: { value: VisitorStatus; label: string }[] = [
    { value: 'Scheduled', label: 'Scheduled' },
    { value: 'CheckedIn', label: 'Checked in' },
    { value: 'Completed', label: 'Completed' },
    { value: 'Cancelled', label: 'Cancelled' },
  ];

  // The datepicker blocks past days when creating. When editing, an old record must stay editable.
  readonly minDate: Date | null = this.isEdit ? null : startOfDay(new Date());
  readonly maxDate = startOfDay(new Date(Date.now() + MAX_FUTURE_DAYS * 86_400_000));

  readonly hosts = signal<Host[]>([]);
  readonly hostsLoading = signal(false);
  readonly purposes = signal<Purpose[]>([]);
  readonly saving = signal(false);
  readonly banner = signal<string | null>(null);
  readonly conflict = signal(false);

  readonly displayHost = (value: Host | string | null): string =>
    typeof value === 'object' && value ? value.fullName : (value ?? '');

  /** The time rule: not in the past and not too far ahead, unless the date and time were left unchanged. */
  private readonly timeValidator = (control: AbstractControl): ValidationErrors | null => {
    const time = control.value as string;
    if (!time) return { required: true };

    const date = control.parent?.get('visitDate')?.value as Date | null;
    const when = combineDateTime(date, time);
    if (!when || this.isUnchanged(when)) return null;

    const now = Date.now();
    if (when.getTime() < now - PAST_GRACE_MS) return { past: true };
    if (when.getTime() > now + MAX_FUTURE_DAYS * 86_400_000) return { tooFar: true };
    return null;
  };

  readonly form = new FormGroup({
    visitorName: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.minLength(2), Validators.maxLength(100)],
    }),
    companyName: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(150)],
    }),
    mobileNumber: new FormControl('', { nonNullable: true, validators: [Validators.required, mobileValidator] }),
    email: new FormControl('', { nonNullable: true, validators: [Validators.email, Validators.maxLength(254)] }),
    host: new FormControl<Host | string | null>(null, [Validators.required, hostSelectedValidator]),
    purposeId: new FormControl<number | null>(null, [Validators.required]),
    visitDate: new FormControl<Date | null>(null, [Validators.required]),
    visitTime: new FormControl('', { nonNullable: true, validators: [this.timeValidator] }),
    status: new FormControl<VisitorStatus>('Scheduled', { nonNullable: true }),
    remarks: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(500)] }),
  });

  private readonly messages: Record<string, Record<string, string>> = {
    visitorName: {
      required: 'Visitor name is required.',
      minlength: 'Visitor name must be at least 2 characters.',
      maxlength: 'Visitor name must not exceed 100 characters.',
    },
    companyName: {
      required: 'Company name is required.',
      maxlength: 'Company name must not exceed 150 characters.',
    },
    mobileNumber: {
      required: 'Mobile number is required.',
      mobile: 'Enter a valid mobile number using exactly 10 digits.',
    },
    email: {
      email: 'Enter a valid email address.',
      maxlength: 'Email address must not exceed 254 characters.',
    },
    host: { required: 'Host is required.', hostNotSelected: 'Pick a host from the list.' },
    purposeId: { required: 'Purpose of visit is required.' },
    visitDate: {
      required: 'Visit date is required.',
      matDatepickerParse: 'Enter a valid date.',
      matDatepickerMin: 'Visit date cannot be in the past.',
      matDatepickerMax: `Visit date cannot be more than ${MAX_FUTURE_DAYS} days ahead.`,
    },
    visitTime: {
      required: 'Visit time is required.',
      past: 'Visit time cannot be in the past.',
      tooFar: `Visit cannot be more than ${MAX_FUTURE_DAYS} days ahead.`,
    },
    remarks: { maxlength: 'Remarks must not exceed 500 characters.' },
  };

  constructor() {
    if (this.visitor) {
      this.patchFrom(this.visitor);
    } else {
      // A sensible default: the next full hour.
      const next = new Date();
      next.setHours(next.getHours() + 1, 0, 0, 0);
      this.form.patchValue({ visitDate: startOfDay(next), visitTime: toTimeString(next) });
    }

    this.api
      .purposes()
      .pipe(takeUntilDestroyed())
      .subscribe({
        next: (list) => this.purposes.set(list),
        error: () => this.banner.set('Purposes could not be loaded. Close the form and try again.'),
      });

    // Host type-ahead: debounce, cancel stale searches, ignore the value set when an option is picked.
    this.form.controls.host.valueChanges
      .pipe(
        startWith(''),
        filter((value): value is string | null => typeof value !== 'object' || value === null),
        map((value) => (value ?? '').trim()),
        debounceTime(250),
        distinctUntilChanged(),
        tap(() => this.hostsLoading.set(true)),
        switchMap((term) => this.api.searchHosts(term).pipe(catchError(() => of<Host[]>([])))),
        takeUntilDestroyed(),
      )
      .subscribe((list) => {
        this.hosts.set(list);
        this.hostsLoading.set(false);
      });

    // The time rule depends on the date, so re-check it whenever the date changes.
    this.form.controls.visitDate.valueChanges
      .pipe(takeUntilDestroyed())
      .subscribe(() => this.form.controls.visitTime.updateValueAndValidity());
  }

  err(name: string): string {
    const errors = this.form.get(name)?.errors;
    if (!errors) return '';
    if (typeof errors['server'] === 'string') return errors['server'];
    const key = Object.keys(errors)[0];
    return this.messages[name]?.[key] ?? 'This value is not valid.';
  }

  close(): void {
    if (!this.saving()) this.dialogRef.close();
  }

  submit(): void {
    if (this.saving()) return;   // blocks a double submit

    this.banner.set(null);
    this.conflict.set(false);

    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const raw = this.form.getRawValue();
    const when = combineDateTime(raw.visitDate, raw.visitTime)!;

    // Unchanged date and time: send the stored value back untouched, so precision is never lost.
    const visitDate = this.visitor && this.isUnchanged(when) ? this.visitor.visitDate : when.toISOString();

    const request: VisitorRequest = {
      visitorName: raw.visitorName.trim(),
      companyName: raw.companyName.trim(),
      mobileNumber: raw.mobileNumber.trim(),
      email: raw.email.trim() || null,
      visitDate,
      hostId: (raw.host as Host).id,
      purposeId: raw.purposeId!,
      remarks: raw.remarks.trim() || null,
    };

    this.saving.set(true);
    this.dialogRef.disableClose = true;

    const call = this.visitor
      ? this.api.update(this.visitor.id, { ...request, status: raw.status, rowVersion: this.visitor.rowVersion })
      : this.api.create(request);

    call.subscribe({
      next: (saved) => {
        this.dialogRef.disableClose = false;
        this.dialogRef.close(saved);
      },
      error: (error: unknown) => {
        this.saving.set(false);
        this.dialogRef.disableClose = false;
        this.showError(toApiError(error));
      },
    });
  }

  reloadLatest(): void {
    if (!this.visitor || this.saving()) return;

    this.saving.set(true);
    this.api.get(this.visitor.id).subscribe({
      next: (latest) => {
        this.visitor = latest;
        this.patchFrom(latest);
        this.conflict.set(false);
        this.banner.set('The latest version has been loaded. Review it and save again.');
        this.saving.set(false);
      },
      error: (error: unknown) => {
        this.saving.set(false);
        this.showError(toApiError(error));
      },
    });
  }

  private showError(error: ReturnType<typeof toApiError>): void {
    let message: string;

    if (error.status === 400 && error.errors) {
      const unmatched = this.applyServerErrors(error.errors);
      message = unmatched.length ? unmatched.join(' ') : 'Please correct the highlighted fields.';
    } else {
      message = error.detail;   // 409, 403, 404, 500 and network messages are already user-friendly
    }

    this.banner.set(message);
    this.snackBar.open(
      `${this.isEdit ? 'Could not update' : 'Could not add'} visitor: ${message}`,
      'Dismiss',
      { duration: 6000 },
    );
    this.conflict.set(error.status === 409);
  }

  /** Puts API field errors on the matching form fields. Returns messages that have no matching field. */
  private applyServerErrors(errors: Record<string, string[]>): string[] {
    const fieldMap: Record<string, string> = {
      visitorName: 'visitorName',
      companyName: 'companyName',
      mobileNumber: 'mobileNumber',
      email: 'email',
      visitDate: 'visitTime',
      hostId: 'host',
      purposeId: 'purposeId',
      remarks: 'remarks',
      status: 'status',
    };

    const unmatched: string[] = [];
    for (const [key, messages] of Object.entries(errors)) {
      const control = this.form.get(fieldMap[key] ?? '');
      if (control) {
        control.setErrors({ server: messages[0] });   // cleared automatically when the user edits the field
        control.markAsTouched();
      } else {
        unmatched.push(...messages);
      }
    }
    return unmatched;
  }

  private patchFrom(visitor: Visitor): void {
    const when = new Date(visitor.visitDate);
    this.form.reset({
      visitorName: visitor.visitorName,
      companyName: visitor.companyName,
      mobileNumber: visitor.mobileNumber.replace(/^\+\d{1,3}/, ''),
      email: visitor.email ?? '',
      host: { id: visitor.hostId, fullName: visitor.hostName, department: '' },
      purposeId: visitor.purposeId,
      visitDate: startOfDay(when),
      visitTime: toTimeString(when),
      status: visitor.status,
      remarks: visitor.remarks ?? '',
    });
  }

  private isUnchanged(when: Date): boolean {
    if (!this.visitor) return false;
    const original = new Date(this.visitor.visitDate).getTime();
    return Math.floor(original / 60_000) === Math.floor(when.getTime() / 60_000);
  }

}