import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { API_BASE_URL } from '../config';
import { ensureUtc } from '../date-utils';
import {
  Host,
  PagedResult,
  Purpose,
  UpdateVisitorRequest,
  Visitor,
  VisitorListQuery,
  VisitorRequest,
} from '../models/api.models';

const normalize = (v: Visitor): Visitor => ({
  ...v,
  visitDate: ensureUtc(v.visitDate)!,
  createdAtUtc: ensureUtc(v.createdAtUtc)!,
  updatedAtUtc: ensureUtc(v.updatedAtUtc),
});

@Injectable({ providedIn: 'root' })
export class VisitorApiService {
  private readonly http = inject(HttpClient);
  private readonly base = `${API_BASE_URL}/api`;

  list(query: VisitorListQuery): Observable<PagedResult<Visitor>> {
    let params = new HttpParams()
      .set('sortBy', query.sortBy)
      .set('sortDirection', query.sortDirection)
      .set('page', query.page)
      .set('pageSize', query.pageSize);
    if (query.search) params = params.set('search', query.search);

    return this.http
      .get<PagedResult<Visitor>>(`${this.base}/visitors`, { params })
      .pipe(map((r) => ({ ...r, items: r.items.map(normalize) })));
  }

  get(id: number): Observable<Visitor> {
    return this.http.get<Visitor>(`${this.base}/visitors/${id}`).pipe(map(normalize));
  }

  create(request: VisitorRequest): Observable<Visitor> {
    return this.http.post<Visitor>(`${this.base}/visitors`, request).pipe(map(normalize));
  }

  update(id: number, request: UpdateVisitorRequest): Observable<Visitor> {
    return this.http.put<Visitor>(`${this.base}/visitors/${id}`, request).pipe(map(normalize));
  }

  searchHosts(term: string): Observable<Host[]> {
    let params = new HttpParams().set('limit', 10);
    if (term) params = params.set('search', term);
    return this.http.get<Host[]>(`${this.base}/hosts`, { params });
  }

  purposes(): Observable<Purpose[]> {
    return this.http.get<Purpose[]>(`${this.base}/purposes`);
  }
}