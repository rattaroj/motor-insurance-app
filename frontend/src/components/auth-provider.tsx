'use client';

import { useEffect } from 'react';
import { usePathname, useRouter } from 'next/navigation';
import { ShieldCheck } from 'lucide-react';
import { useAppDispatch, useAppSelector } from '@/lib/store/store';
import { useRefreshMutation } from '@/lib/api/insuranceApi';
import { setCredentials, clearCredentials } from '@/lib/auth/authSlice';
import { requiredPermission } from '@/lib/auth/permissions';

const PUBLIC_PATHS = ['/login'];

/**
 * Bootstraps the session on load via a silent /auth/refresh (reads the httpOnly cookie),
 * then enforces route access: redirects unauthenticated users to /login, sends
 * authenticated users away from /login, and blocks pages the user lacks permission for.
 */
export function AuthProvider({ children }: { children: React.ReactNode }) {
  const dispatch = useAppDispatch();
  const router = useRouter();
  const pathname = usePathname();
  const { status, permissions } = useAppSelector((s) => ({
    status: s.auth.status,
    permissions: s.auth.user?.permissions ?? [],
  }));
  const [refresh] = useRefreshMutation();

  // One-time silent bootstrap.
  useEffect(() => {
    let active = true;
    (async () => {
      try {
        const res = await refresh().unwrap();
        if (active) dispatch(setCredentials(res));
      } catch {
        if (active) dispatch(clearCredentials());
      }
    })();
    return () => {
      active = false;
    };
  }, [dispatch, refresh]);

  const isPublic = PUBLIC_PATHS.includes(pathname);
  const required = requiredPermission(pathname);
  const lacksPermission =
    status === 'authenticated' && !isPublic && !!required && !permissions.includes(required);

  // Route guards (run after status settles).
  useEffect(() => {
    if (status === 'idle') return;
    if (status === 'unauthenticated' && !isPublic) router.replace('/login');
    else if (status === 'authenticated' && isPublic) router.replace('/');
    else if (lacksPermission) router.replace('/');
  }, [status, isPublic, lacksPermission, router]);

  // Splash while bootstrapping or while a redirect is pending (avoids content flash).
  const redirecting =
    (status === 'unauthenticated' && !isPublic) ||
    (status === 'authenticated' && isPublic) ||
    lacksPermission;

  if (status === 'idle' || redirecting) {
    return (
      <div className="flex min-h-screen items-center justify-center bg-background">
        <div className="flex flex-col items-center gap-3 text-muted-foreground">
          <ShieldCheck className="h-8 w-8 animate-pulse text-primary" />
          <p className="text-sm">กำลังโหลด…</p>
        </div>
      </div>
    );
  }

  return <>{children}</>;
}
