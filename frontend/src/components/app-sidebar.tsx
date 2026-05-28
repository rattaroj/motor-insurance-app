'use client';

import Link from 'next/link';
import { usePathname } from 'next/navigation';
import {
  LayoutDashboard,
  Users,
  Car,
  FileText,
  ShieldCheck,
  FileWarning,
  CreditCard,
  Database,
  type LucideIcon,
} from 'lucide-react';
import { cn } from '@/lib/utils';

type NavItem = { href: string; label: string; icon: LucideIcon };

const nav: NavItem[] = [
  { href: '/', label: 'แดชบอร์ด', icon: LayoutDashboard },
  { href: '/customers', label: 'ลูกค้า', icon: Users },
  { href: '/vehicles', label: 'รถยนต์', icon: Car },
  { href: '/quotations', label: 'ใบเสนอราคา', icon: FileText },
  { href: '/policies', label: 'กรมธรรม์', icon: ShieldCheck },
  { href: '/claims', label: 'เคลม', icon: FileWarning },
  { href: '/payments', label: 'การชำระเงิน', icon: CreditCard },
  { href: '/master', label: 'ข้อมูลหลักรถยนต์', icon: Database },
];

export function SidebarNav({ collapsed = false, onNavigate }: { collapsed?: boolean; onNavigate?: () => void }) {
  const pathname = usePathname();
  const isActive = (href: string) => (href === '/' ? pathname === '/' : pathname.startsWith(href));

  return (
    <div
      className={cn(
        'flex h-full flex-col bg-sidebar text-sidebar-foreground transition-[width] duration-200',
        collapsed ? 'w-16' : 'w-64',
      )}
    >
      <div className={cn('flex items-center gap-2 py-5', collapsed ? 'justify-center px-0' : 'px-6')}>
        <ShieldCheck className="h-6 w-6 shrink-0 text-sidebar-accent" />
        {!collapsed && (
          <div className="leading-tight">
            <p className="text-sm font-semibold">Motor Insurance</p>
            <p className="text-xs text-sidebar-foreground/60">ระบบประกันรถยนต์</p>
          </div>
        )}
      </div>

      <nav className={cn('flex-1 space-y-1 py-2', collapsed ? 'px-2' : 'px-3')}>
        {nav.map(({ href, label, icon: Icon }) => (
          <Link
            key={href}
            href={href}
            onClick={onNavigate}
            title={collapsed ? label : undefined}
            className={cn(
              'group relative flex items-center rounded-lg text-sm font-medium transition-colors',
              collapsed ? 'justify-center px-0 py-2.5' : 'gap-3 px-3 py-2.5',
              isActive(href)
                ? 'bg-sidebar-accent text-sidebar-accent-foreground'
                : 'text-sidebar-foreground/70 hover:bg-white/10 hover:text-sidebar-foreground',
            )}
          >
            <Icon className="h-4 w-4 shrink-0" />
            {!collapsed && <span className="truncate">{label}</span>}
            {collapsed && (
              <span className="pointer-events-none absolute left-full top-1/2 z-50 ml-2 -translate-y-1/2 whitespace-nowrap rounded-md bg-popover px-2.5 py-1.5 text-xs font-medium text-popover-foreground opacity-0 shadow-md ring-1 ring-border transition-opacity duration-150 group-hover:opacity-100">
                {label}
              </span>
            )}
          </Link>
        ))}
      </nav>

      {!collapsed && (
        <div className="border-t border-sidebar-border px-6 py-4 text-xs text-sidebar-foreground/60">
          workflow: quote → policy → claim → payment
        </div>
      )}
    </div>
  );
}
