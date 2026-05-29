'use client';

import { useHasPermission } from '@/lib/auth/permissions';

/**
 * Renders `children` only when the logged-in user holds `permission`
 * (or when no permission is required). Use to hide mutation buttons the
 * user cannot perform — the same permission the API enforces server-side.
 */
export function Can({
  permission,
  children,
  fallback = null,
}: {
  permission?: string;
  children: React.ReactNode;
  fallback?: React.ReactNode;
}) {
  return <>{useHasPermission(permission) ? children : fallback}</>;
}
