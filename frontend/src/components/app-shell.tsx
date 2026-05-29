'use client';

import { useState } from 'react';
import { usePathname } from 'next/navigation';
import { Menu } from 'lucide-react';
import { SidebarNav } from '@/components/app-sidebar';
import { UserMenu } from '@/components/user-menu';
import { cn } from '@/lib/utils';

export function AppShell({ children }: { children: React.ReactNode }) {
  const pathname = usePathname();
  const [collapsed, setCollapsed] = useState(false); // desktop: icon rail
  const [mobileOpen, setMobileOpen] = useState(false); // mobile: drawer

  // The login page has no app chrome (sidebar/header).
  if (pathname === '/login') return <>{children}</>;

  const toggle = () => {
    if (typeof window !== 'undefined' && window.matchMedia('(min-width: 1024px)').matches) {
      setCollapsed((c) => !c);
    } else {
      setMobileOpen((o) => !o);
    }
  };

  return (
    <div className="flex min-h-screen">
      {/* Desktop sidebar (collapses to an icon rail) */}
      <aside className="relative z-40 hidden shrink-0 lg:block">
        <div className="sticky top-0 h-screen">
          <SidebarNav collapsed={collapsed} />
        </div>
      </aside>

      {/* Mobile drawer */}
      <div className={cn('lg:hidden', !mobileOpen && 'pointer-events-none')}>
        <div
          className={cn(
            'fixed inset-0 z-40 bg-slate-900/50 transition-opacity duration-200',
            mobileOpen ? 'opacity-100' : 'opacity-0',
          )}
          onClick={() => setMobileOpen(false)}
          aria-hidden
        />
        <div
          className={cn(
            'fixed inset-y-0 left-0 z-50 transition-transform duration-200 ease-in-out',
            mobileOpen ? 'translate-x-0' : '-translate-x-full',
          )}
        >
          <SidebarNav onNavigate={() => setMobileOpen(false)} />
        </div>
      </div>

      {/* Main area */}
      <div className="flex min-w-0 flex-1 flex-col">
        <header className="sticky top-0 z-30 flex h-14 items-center gap-3 border-b bg-background/95 px-4 backdrop-blur supports-[backdrop-filter]:bg-background/80">
          <button
            onClick={toggle}
            className="rounded-md p-2 text-muted-foreground transition-colors hover:bg-muted hover:text-foreground"
            aria-label="เปิด/ปิดเมนู"
          >
            <Menu className="h-5 w-5" />
          </button>
          <span className="font-semibold">ระบบประกันรถยนต์</span>
          <div className="ml-auto">
            <UserMenu />
          </div>
        </header>
        <main className="flex-1 overflow-x-hidden">
          <div className="mx-auto max-w-6xl px-4 py-6 sm:px-6 sm:py-8">{children}</div>
        </main>
      </div>
    </div>
  );
}
