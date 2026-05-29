'use client';

import { useEffect, useRef, useState } from 'react';
import { useRouter } from 'next/navigation';
import { ChevronDown, LogOut, UserRound } from 'lucide-react';
import { useAppDispatch, useAppSelector } from '@/lib/store/store';
import { useLogoutMutation } from '@/lib/api/insuranceApi';
import { clearCredentials } from '@/lib/auth/authSlice';
import { cn } from '@/lib/utils';

const ROLE_LABEL: Record<string, string> = {
  ADMIN: 'ผู้ดูแลระบบ',
  UNDERWRITER: 'เจ้าหน้าที่รับประกัน',
  CLAIMS: 'เจ้าหน้าที่สินไหม',
  FINANCE: 'เจ้าหน้าที่การเงิน',
  VIEWER: 'ผู้ดูข้อมูล',
};

export function UserMenu() {
  const dispatch = useAppDispatch();
  const router = useRouter();
  const user = useAppSelector((s) => s.auth.user);
  const [logout, { isLoading }] = useLogoutMutation();
  const [open, setOpen] = useState(false);
  const ref = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const onClick = (e: MouseEvent) => {
      if (ref.current && !ref.current.contains(e.target as Node)) setOpen(false);
    };
    document.addEventListener('mousedown', onClick);
    return () => document.removeEventListener('mousedown', onClick);
  }, []);

  if (!user) return null;

  const roleLabel = user.roles.map((r) => ROLE_LABEL[r] ?? r).join(', ');

  const signOut = async () => {
    try {
      await logout().unwrap();
    } catch {
      // ignore — clear locally regardless
    }
    dispatch(clearCredentials());
    router.replace('/login');
  };

  return (
    <div ref={ref} className="relative">
      <button
        onClick={() => setOpen((o) => !o)}
        className="flex items-center gap-2 rounded-md px-2 py-1.5 text-sm transition-colors hover:bg-muted"
      >
        <span className="flex h-8 w-8 items-center justify-center rounded-full bg-primary/10 text-primary">
          <UserRound className="h-4 w-4" />
        </span>
        <span className="hidden text-left leading-tight sm:block">
          <span className="block font-medium">{user.fullName}</span>
          <span className="block text-xs text-muted-foreground">{roleLabel}</span>
        </span>
        <ChevronDown className={cn('h-4 w-4 text-muted-foreground transition-transform', open && 'rotate-180')} />
      </button>

      {open && (
        <div className="absolute right-0 z-50 mt-2 w-56 overflow-hidden rounded-md border bg-popover p-1 shadow-md">
          <div className="px-3 py-2">
            <p className="text-sm font-medium">{user.fullName}</p>
            <p className="text-xs text-muted-foreground">@{user.username}</p>
            <p className="mt-1 text-xs text-muted-foreground">{roleLabel}</p>
          </div>
          <div className="my-1 h-px bg-border" />
          <button
            onClick={signOut}
            disabled={isLoading}
            className="flex w-full items-center gap-2 rounded-sm px-3 py-2 text-sm text-destructive transition-colors hover:bg-destructive/10 disabled:opacity-50"
          >
            <LogOut className="h-4 w-4" />
            {isLoading ? 'กำลังออก…' : 'ออกจากระบบ'}
          </button>
        </div>
      )}
    </div>
  );
}
