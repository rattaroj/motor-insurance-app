'use client';

import { useState } from 'react';
import Link from 'next/link';
import { usePathname } from 'next/navigation';
import {
  LayoutDashboard,
  Users,
  Car,
  FileText,
  ShieldCheck,
  ShieldPlus,
  FileWarning,
  CreditCard,
  Database,
  BarChart3,
  CalendarClock,
  UserSquare2,
  Wrench,
  ChevronDown,
  type LucideIcon,
} from 'lucide-react';
import { cn } from '@/lib/utils';
import { P, usePermissions } from '@/lib/auth/permissions';

type NavChild = { href: string; label: string; icon: LucideIcon };
type NavItem = {
  href: string;
  label: string;
  icon: LucideIcon;
  permission: string;
  children?: NavChild[];
};

const nav: NavItem[] = [
  { href: '/', label: 'แดชบอร์ด', icon: LayoutDashboard, permission: P.DashboardRead },
  { href: '/customers', label: 'ลูกค้า', icon: Users, permission: P.CustomerRead },
  { href: '/vehicles', label: 'รถยนต์', icon: Car, permission: P.VehicleRead },
  { href: '/quotations', label: 'ใบเสนอราคา', icon: FileText, permission: P.QuotationRead },
  { href: '/policies', label: 'กรมธรรม์', icon: ShieldCheck, permission: P.PolicyRead },
  { href: '/renewals', label: 'ต่ออายุ', icon: CalendarClock, permission: P.PolicyRenew },
  { href: '/claims', label: 'เคลม', icon: FileWarning, permission: P.ClaimRead },
  { href: '/payments', label: 'การชำระเงิน', icon: CreditCard, permission: P.PaymentRead },
  { href: '/reports', label: 'รายงาน', icon: BarChart3, permission: P.DashboardRead },
  {
    href: '/master',
    label: 'ข้อมูลหลัก',
    icon: Database,
    permission: P.LookupManage,
    children: [
      { href: '/master/vehicles', label: 'ข้อมูลรถยนต์', icon: Car },
      { href: '/master/titles', label: 'คำนำหน้าชื่อ', icon: UserSquare2 },
      { href: '/master/riders', label: 'ความคุ้มครองเสริม', icon: ShieldPlus },
      { href: '/master/garages', label: 'อู่/ศูนย์ซ่อม', icon: Wrench },
    ],
  },
];

export function SidebarNav({ collapsed = false, onNavigate }: { collapsed?: boolean; onNavigate?: () => void }) {
  const pathname = usePathname();
  const permissions = usePermissions();
  const items = nav.filter((item) => permissions.includes(item.permission));
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

      <nav className={cn('flex-1 space-y-1 overflow-y-auto py-2', collapsed ? 'px-2' : 'px-3')}>
        {items.map((item) =>
          item.children && !collapsed ? (
            <NavGroup key={item.href} item={item} pathname={pathname} onNavigate={onNavigate} />
          ) : (
            <NavLink
              key={item.href}
              href={item.href}
              label={item.label}
              icon={item.icon}
              active={isActive(item.href)}
              collapsed={collapsed}
              onNavigate={onNavigate}
            />
          ),
        )}
      </nav>

      {!collapsed && (
        <div className="border-t border-sidebar-border px-6 py-4 text-xs text-sidebar-foreground/60">
          workflow: quote → policy → claim → payment
        </div>
      )}
    </div>
  );
}

function NavLink({
  href,
  label,
  icon: Icon,
  active,
  collapsed,
  onNavigate,
}: {
  href: string;
  label: string;
  icon: LucideIcon;
  active: boolean;
  collapsed: boolean;
  onNavigate?: () => void;
}) {
  return (
    <Link
      href={href}
      onClick={onNavigate}
      title={collapsed ? label : undefined}
      className={cn(
        'group relative flex items-center rounded-lg text-sm font-medium transition-colors',
        collapsed ? 'justify-center px-0 py-2.5' : 'gap-3 px-3 py-2.5',
        active
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
  );
}

/** Expandable parent with sub-items (e.g. ข้อมูลหลัก). Auto-opens on a child route. */
function NavGroup({
  item,
  pathname,
  onNavigate,
}: {
  item: NavItem;
  pathname: string;
  onNavigate?: () => void;
}) {
  const sectionActive = pathname.startsWith(item.href);
  const [open, setOpen] = useState(sectionActive);
  const Icon = item.icon;

  return (
    <div>
      <button
        type="button"
        onClick={() => setOpen((o) => !o)}
        aria-expanded={open}
        className={cn(
          'flex w-full items-center gap-3 rounded-lg px-3 py-2.5 text-sm font-medium transition-colors',
          sectionActive
            ? 'text-sidebar-foreground'
            : 'text-sidebar-foreground/70 hover:bg-white/10 hover:text-sidebar-foreground',
        )}
      >
        <Icon className="h-4 w-4 shrink-0" />
        <span className="flex-1 truncate text-left">{item.label}</span>
        <ChevronDown className={cn('h-4 w-4 shrink-0 transition-transform', open && 'rotate-180')} />
      </button>

      {open && (
        <div className="mt-1 space-y-1 border-l border-sidebar-border/60 pl-3">
          {item.children!.map((child) => {
            const active = pathname === child.href || pathname.startsWith(`${child.href}/`);
            const ChildIcon = child.icon;
            return (
              <Link
                key={child.href}
                href={child.href}
                onClick={onNavigate}
                className={cn(
                  'flex items-center gap-2.5 rounded-lg px-3 py-2 text-sm transition-colors',
                  active
                    ? 'bg-sidebar-accent text-sidebar-accent-foreground font-medium'
                    : 'text-sidebar-foreground/70 hover:bg-white/10 hover:text-sidebar-foreground',
                )}
              >
                <ChildIcon className="h-3.5 w-3.5 shrink-0" />
                <span className="truncate">{child.label}</span>
              </Link>
            );
          })}
        </div>
      )}
    </div>
  );
}
