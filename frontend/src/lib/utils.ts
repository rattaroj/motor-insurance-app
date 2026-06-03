import { clsx, type ClassValue } from 'clsx';
import { twMerge } from 'tailwind-merge';

export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs));
}

export const fmtBaht = (n: number) =>
  new Intl.NumberFormat('th-TH', { style: 'currency', currency: 'THB', maximumFractionDigits: 0 }).format(n);

export const fmtDate = (s?: string | null) =>
  s ? new Date(s).toLocaleDateString('th-TH', { year: 'numeric', month: 'short', day: 'numeric' }) : '-';

export const fmtDateTime = (s?: string | null) =>
  s ? new Date(s).toLocaleString('th-TH', { dateStyle: 'medium', timeStyle: 'short' }) : '-';

/** Triggers a browser download of an object URL (e.g. a generated PDF) and revokes it. */
export function saveUrl(url: string, filename: string) {
  const a = document.createElement('a');
  a.href = url;
  a.download = filename;
  document.body.appendChild(a);
  a.click();
  a.remove();
  URL.revokeObjectURL(url);
}

/** Extracts a human message from an RTK Query error (ApiResponse envelope or ProblemDetails). */
export function apiError(e: unknown): string {
  const err = e as { data?: { message?: string; title?: string }; error?: string; status?: number };
  return err?.data?.message ?? err?.data?.title ?? err?.error ?? 'เกิดข้อผิดพลาด กรุณาลองใหม่';
}
