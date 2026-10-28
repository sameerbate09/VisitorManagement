import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, map, tap } from 'rxjs';
import { API_BASE_URL } from '../config';
import { LoginRequest, LoginResponse, Role } from '../models/api.models';

export type SignOutReason = 'expired' | 'signed-out';

export interface Session {
  token: string;
  expiresAtUtc: string;
  username: string;
  displayName: string;
  role: Role;
}

const STORAGE_KEY = 'vm.session';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly state = signal<Session | null>(null);
  private expiryTimer: ReturnType<typeof setTimeout> | undefined;

  readonly session = this.state.asReadonly();
  readonly isAdmin = computed(() => this.state()?.role === 'Admin');

  constructor() {
    const stored = this.readStored();
    if (stored && !this.isExpired(stored)) {
      this.state.set(stored);
      this.scheduleExpiry(stored);
    } else {
      this.clearStored();
    }
  }

  /** The token, but only while it is still valid. */
  getToken(): string | null {
    const session = this.state();
    return session && !this.isExpired(session) ? session.token : null;
  }

  isAuthenticated(): boolean {
    return this.getToken() !== null;
  }

  login(request: LoginRequest): Observable<Session> {
    return this.http.post<LoginResponse>(`${API_BASE_URL}/api/auth/login`, request).pipe(
      map((r): Session => ({
        token: r.token,
        expiresAtUtc: r.expiresAtUtc,
        username: r.username,
        displayName: r.displayName,
        role: r.role,
      })),
      tap((session) => {
        this.state.set(session);
        this.writeStored(session);
        this.scheduleExpiry(session);
      }),
    );
  }

  signOut(reason: SignOutReason = 'signed-out'): void {
    clearTimeout(this.expiryTimer);
    this.state.set(null);
    this.clearStored();
    void this.router.navigate(['/login'], { queryParams: reason === 'expired' ? { reason } : {} });
  }

  private isExpired(session: Session): boolean {
    return !(Date.parse(session.expiresAtUtc) > Date.now());
  }

  private scheduleExpiry(session: Session): void {
    clearTimeout(this.expiryTimer);
    const ms = Math.min(Math.max(Date.parse(session.expiresAtUtc) - Date.now(), 0), 2_147_000_000);
    this.expiryTimer = setTimeout(() => this.signOut('expired'), ms);
  }

  private readStored(): Session | null {
    try {
      const raw = sessionStorage.getItem(STORAGE_KEY);
      if (!raw) return null;
      const parsed: unknown = JSON.parse(raw);
      return this.isSession(parsed) ? parsed : null;
    } catch {
      return null;
    }
  }

  private writeStored(session: Session): void {
    try {
      sessionStorage.setItem(STORAGE_KEY, JSON.stringify(session));
    } catch {
      /* storage unavailable: the session still works until a page refresh */
    }
  }

  private clearStored(): void {
    try {
      sessionStorage.removeItem(STORAGE_KEY);
    } catch {
      /* ignore */
    }
  }

  private isSession(value: unknown): value is Session {
    const s = value as Session | null;
    return (
      !!s &&
      typeof s.token === 'string' &&
      typeof s.expiresAtUtc === 'string' &&
      typeof s.username === 'string' &&
      typeof s.displayName === 'string' &&
      (s.role === 'Admin' || s.role === 'User')
    );
  }
}