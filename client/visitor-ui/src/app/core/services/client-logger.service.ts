import { Injectable } from '@angular/core';

type Level = 'info' | 'warn' | 'error';

/** Structured UI logging: Source=UI plus the shared correlation ID. Never pass tokens or passwords. */
@Injectable({ providedIn: 'root' })
export class ClientLogger {
  info(operation: string, message: string, correlationId?: string): void {
    this.write('info', operation, message, correlationId);
  }

  warn(operation: string, message: string, correlationId?: string): void {
    this.write('warn', operation, message, correlationId);
  }

  error(operation: string, message: string, correlationId?: string): void {
    this.write('error', operation, message, correlationId);
  }

  private write(level: Level, operation: string, message: string, correlationId?: string): void {
    const entry = {
      timestamp: new Date().toISOString(),
      level,
      source: 'UI',
      correlationId: correlationId ?? null,
      operation,
      message,
    };
    console[level](`[UI] [${entry.correlationId ?? '-'}] ${operation}: ${message}`, entry);
  }
}