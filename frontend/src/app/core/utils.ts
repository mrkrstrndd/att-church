import { HttpErrorResponse } from '@angular/common/http';
import { ProblemDetails } from './api';

export function apiErrorMessage(error: unknown, fallback = 'Something went wrong. Please try again.'): string {
  if (error instanceof HttpErrorResponse) {
    const problem = error.error as ProblemDetails | null;
    if (problem?.errors) {
      const first = Object.values(problem.errors)
        .flat()
        .find((message): message is string => typeof message === 'string' && !!message);
      if (first) {
        return first;
      }
    }
    if (problem?.detail) {
      return problem.detail;
    }
    if (problem?.title) {
      return problem.title;
    }
    if (error.status === 0) {
      return 'The server is unreachable. Please check your connection.';
    }
    if (error.status === 401) {
      return 'Invalid username or password.';
    }
    if (error.status === 403) {
      return 'You do not have permission to perform this action.';
    }
  }

  if (error instanceof Error && error.message) {
    return error.message;
  }
  return fallback;
}

export function formatDate(value: string | null | undefined, options?: Intl.DateTimeFormatOptions): string {
  if (!value) {
    return '—';
  }
  const date = new Date(`${value}T00:00:00`);
  if (Number.isNaN(date.getTime())) {
    return value;
  }
  return new Intl.DateTimeFormat('en-US', options ?? { year: 'numeric', month: 'short', day: 'numeric' }).format(date);
}

export function formatDateTime(value: string | null | undefined): string {
  if (!value) {
    return '—';
  }
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return value;
  }
  return new Intl.DateTimeFormat('en-US', {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
    hour: 'numeric',
    minute: '2-digit'
  }).format(date);
}

export function toIsoDate(date: Date | null): string {
  if (!date) {
    return '';
  }
  const year = date.getFullYear();
  const month = `${date.getMonth() + 1}`.padStart(2, '0');
  const day = `${date.getDate()}`.padStart(2, '0');
  return `${year}-${month}-${day}`;
}

export function fromIsoDate(value: string | null | undefined): Date | null {
  if (!value) {
    return null;
  }
  const date = new Date(`${value}T00:00:00`);
  return Number.isNaN(date.getTime()) ? null : date;
}
