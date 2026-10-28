
import { DatePipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule } from '@angular/forms';

import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import {
  MatPaginatorModule,
  PageEvent,
} from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import {
  MatSortModule,
  Sort,
} from '@angular/material/sort';
import { MatTableModule } from '@angular/material/table';

import {
  Subject,
  catchError,
  debounceTime,
  distinctUntilChanged,
  map,
  of,
  startWith,
  switchMap,
  tap,
} from 'rxjs';

import { toApiError } from '../../core/models/api-error';
import {
  ApiError,
  PagedResult,
  Visitor,
  VisitorListQuery,
  VisitorStatus,
} from '../../core/models/api.models';
import { AuthService } from '../../core/services/auth.service';
import { VisitorApiService } from '../../core/services/visitor-api.service';

interface LoadOutcome {
  result: PagedResult<Visitor> | null;
  error: ApiError | null;
}

@Component({
  selector: 'app-visitor-list-page',
  standalone: true,
  imports: [
    DatePipe,
    ReactiveFormsModule,
    MatTableModule,
    MatSortModule,
    MatPaginatorModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatIconModule,
    MatProgressBarModule,
  ],
  templateUrl: './visitor-list-page.component.html',
  styleUrl: './visitor-list-page.component.scss',
})
export class VisitorListPageComponent {
  private readonly api = inject(VisitorApiService);

  readonly auth = inject(AuthService);

  readonly columns = [
    'visitorName',
    'companyName',
    'hostName',
    'purposeName',
    'visitDate',
    'status',
  ];

  readonly skeletonRows = [1, 2, 3, 4, 5];

  readonly searchControl = new FormControl('', {
    nonNullable: true,
  });

  readonly items = signal<Visitor[]>([]);
  readonly total = signal(0);
  readonly page = signal(1);
  readonly pageSize = signal(10);
  readonly sortBy = signal('visitDate');
  readonly sortDirection = signal<'asc' | 'desc'>('desc');
  readonly loading = signal(false);
  readonly hasLoaded = signal(false);
  readonly error = signal<ApiError | null>(null);

  private readonly refresh$ = new Subject<void>();

  constructor() {
    // Cancel older requests when a newer request starts.
    // This prevents results from arriving out of order.
    this.refresh$
      .pipe(
        startWith(undefined),
        tap(() => {
          this.loading.set(true);
          this.error.set(null);
        }),
        switchMap(() =>
          this.api.list(this.currentQuery()).pipe(
            map(
              (result): LoadOutcome => ({
                result,
                error: null,
              }),
            ),
            catchError((e: unknown) =>
              of<LoadOutcome>({
                result: null,
                error: toApiError(e),
              }),
            ),
          ),
        ),
        takeUntilDestroyed(),
      )
      .subscribe(({ result, error }) => {
        this.loading.set(false);
        this.hasLoaded.set(true);

        if (error) {
          this.error.set(error);
          return;
        }

        if (result) {
          // If the current page no longer exists,
          // move back to the last available page.
          if (
            result.items.length === 0 &&
            result.totalCount > 0 &&
            this.page() > 1
          ) {
            this.page.set(Math.max(1, result.totalPages));
            this.refresh$.next();
            return;
          }

          this.items.set(result.items);
          this.total.set(result.totalCount);
        }
      });

    // Debounce search input to avoid calling the API
    // on every keystroke.
    this.searchControl.valueChanges
      .pipe(
        debounceTime(300),
        map((value) => value.trim()),
        distinctUntilChanged(),
        takeUntilDestroyed(),
      )
      .subscribe(() => {
        this.page.set(1);
        this.refresh$.next();
      });
  }

  reload(): void {
    this.refresh$.next();
  }

  clearSearch(): void {
    this.searchControl.setValue('');
  }

  onSort(sort: Sort): void {
    this.sortBy.set(sort.active);
    this.sortDirection.set(
      sort.direction === 'asc' ? 'asc' : 'desc',
    );

    this.page.set(1);
    this.refresh$.next();
  }

  onPage(event: PageEvent): void {
    this.page.set(event.pageIndex + 1);
    this.pageSize.set(event.pageSize);

    this.refresh$.next();
  }

  initials(name: string): string {
    const parts = name.trim().split(/\s+/).filter(Boolean);

    const letters =
      (parts[0]?.[0] ?? '') +
      (parts.length > 1 ? parts[parts.length - 1][0] : '');

    return letters.toUpperCase();
  }

  avatarColor(name: string): string {
    let hash = 0;

    for (const ch of name) {
      hash = (hash * 31 + ch.charCodeAt(0)) % 360;
    }

    return `hsl(${hash}, 55%, 42%)`;
  }

  statusLabel(status: VisitorStatus): string {
    return status === 'CheckedIn' ? 'Checked in' : status;
  }

  whenHint(iso: string): string {
    const visit = new Date(iso);
    const today = new Date();

    const dayStart = (d: Date) =>
      new Date(
        d.getFullYear(),
        d.getMonth(),
        d.getDate(),
      ).getTime();

    const diffDays = Math.round(
      (dayStart(visit) - dayStart(today)) / 86_400_000,
    );

    return diffDays === 0
      ? 'Today'
      : diffDays === 1
        ? 'Tomorrow'
        : '';
  }

  private currentQuery(): VisitorListQuery {
    return {
      search: this.searchControl.value.trim(),
      sortBy: this.sortBy(),
      sortDirection: this.sortDirection(),
      page: this.page(),
      pageSize: this.pageSize(),
    };
  }
}
