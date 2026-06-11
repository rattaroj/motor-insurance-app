'use client';

import { useEffect, useMemo, useRef, useState } from 'react';
import { useRouter } from 'next/navigation';
import {
  Search,
  FileText,
  ShieldCheck,
  User,
  LayoutDashboard,
  Users,
  Car,
  FileWarning,
  CreditCard,
  CalendarClock,
  BarChart3,
  CornerDownLeft,
  type LucideIcon,
} from 'lucide-react';
import { useGlobalSearchQuery, type SearchHit } from '@/lib/api/insuranceApi';
import { useDebouncedValue } from '@/lib/use-debounced';
import { usePermissions, requiredPermission } from '@/lib/auth/permissions';
import { cn } from '@/lib/utils';

const ICON = { policy: ShieldCheck, claim: FileText, customer: User };
const HREF: Record<SearchHit['type'], (id: number) => string> = {
  policy: (id) => `/policies/${id}`,
  customer: (id) => `/customers/${id}`,
  claim: (id) => `/claims/${id}`,
};

/** Quick navigation shown while the query is empty (filtered by route permission). */
const QUICK_LINKS: { href: string; label: string; icon: LucideIcon }[] = [
  { href: '/', label: 'แดชบอร์ด', icon: LayoutDashboard },
  { href: '/customers', label: 'ลูกค้า', icon: Users },
  { href: '/vehicles', label: 'รถยนต์', icon: Car },
  { href: '/quotations/new', label: 'สร้างใบเสนอราคา', icon: FileText },
  { href: '/policies', label: 'กรมธรรม์', icon: ShieldCheck },
  { href: '/renewals', label: 'ต่ออายุ', icon: CalendarClock },
  { href: '/claims', label: 'เคลม', icon: FileWarning },
  { href: '/payments', label: 'การชำระเงิน', icon: CreditCard },
  { href: '/reports', label: 'รายงาน', icon: BarChart3 },
];

type Item = { key: string; icon: LucideIcon; title: string; subtitle?: string; href: string };

/** Header quick-find across policies, claims and customers — opens with Ctrl+K. */
export function GlobalSearch() {
  const router = useRouter();
  const inputRef = useRef<HTMLInputElement>(null);
  const [term, setTerm] = useState('');
  const [open, setOpen] = useState(false);
  const [active, setActive] = useState(0);
  const q = useDebouncedValue(term.trim(), 250);
  const permissions = usePermissions();

  const { data, isFetching } = useGlobalSearchQuery(q, { skip: q.length < 2 });

  // Ctrl+K / Cmd+K focuses the search from anywhere.
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'k') {
        e.preventDefault();
        inputRef.current?.focus();
        setOpen(true);
      }
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, []);

  const items: Item[] = useMemo(() => {
    if (q.length >= 2) {
      const hits: SearchHit[] = data ? [...data.policies, ...data.claims, ...data.customers] : [];
      return hits.map((hit) => ({
        key: `${hit.type}-${hit.id}`,
        icon: ICON[hit.type],
        title: hit.title,
        subtitle: hit.subtitle,
        href: HREF[hit.type](hit.id),
      }));
    }
    return QUICK_LINKS.filter((l) => {
      const required = requiredPermission(l.href);
      return !required || permissions.includes(required);
    }).map((l) => ({ key: l.href, icon: l.icon, title: l.label, href: l.href }));
  }, [q, data, permissions]);

  // Reset highlight whenever the visible list changes.
  useEffect(() => setActive(0), [q, data]);

  const go = (href: string) => {
    setOpen(false);
    setTerm('');
    inputRef.current?.blur();
    router.push(href);
  };

  const onKeyDown = (e: React.KeyboardEvent) => {
    if (!open) return;
    if (e.key === 'ArrowDown') {
      e.preventDefault();
      setActive((a) => (items.length ? (a + 1) % items.length : 0));
    } else if (e.key === 'ArrowUp') {
      e.preventDefault();
      setActive((a) => (items.length ? (a - 1 + items.length) % items.length : 0));
    } else if (e.key === 'Enter') {
      const item = items[active];
      if (item) {
        e.preventDefault();
        go(item.href);
      }
    } else if (e.key === 'Escape') {
      setOpen(false);
      inputRef.current?.blur();
    }
  };

  return (
    <div className="relative w-40 shrink-0 sm:w-64">
      <Search className="pointer-events-none absolute left-2.5 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
      <input
        ref={inputRef}
        type="search"
        value={term}
        onChange={(e) => {
          setTerm(e.target.value);
          setOpen(true);
        }}
        onFocus={() => setOpen(true)}
        // Delay so a click on a result registers before the panel closes.
        onBlur={() => setTimeout(() => setOpen(false), 150)}
        onKeyDown={onKeyDown}
        placeholder="ค้นหากรมธรรม์ / เคลม / ลูกค้า"
        className="h-9 w-full rounded-md border bg-background pl-8 pr-12 text-sm outline-none focus:ring-2 focus:ring-ring"
      />
      <kbd className="pointer-events-none absolute right-2 top-1/2 hidden -translate-y-1/2 rounded border bg-muted px-1.5 py-0.5 text-[10px] font-medium text-muted-foreground sm:inline-block">
        Ctrl K
      </kbd>

      {open && (
        <div className="absolute left-0 right-0 top-11 z-50 overflow-hidden rounded-md border bg-popover shadow-md">
          {q.length >= 2 && isFetching && items.length === 0 ? (
            <p className="px-3 py-3 text-sm text-muted-foreground">กำลังค้นหา…</p>
          ) : items.length === 0 ? (
            <p className="px-3 py-3 text-sm text-muted-foreground">ไม่พบผลลัพธ์</p>
          ) : (
            <>
              {q.length < 2 && (
                <p className="px-3 pb-1 pt-2 text-[11px] font-medium uppercase tracking-wide text-muted-foreground/70">
                  ไปที่
                </p>
              )}
              <ul className="max-h-80 overflow-auto py-1">
                {items.map((item, i) => {
                  const Icon = item.icon;
                  return (
                    <li key={item.key}>
                      <button
                        type="button"
                        onMouseDown={(e) => e.preventDefault()} // keep focus until click handler runs
                        onMouseEnter={() => setActive(i)}
                        onClick={() => go(item.href)}
                        className={cn(
                          'flex w-full items-center gap-2.5 px-3 py-2 text-left text-sm',
                          i === active && 'bg-accent text-accent-foreground',
                        )}
                      >
                        <Icon className="h-4 w-4 shrink-0 text-muted-foreground" />
                        <span className="font-medium">{item.title}</span>
                        {item.subtitle && <span className="truncate text-muted-foreground">{item.subtitle}</span>}
                        {i === active && (
                          <CornerDownLeft className="ml-auto h-3.5 w-3.5 shrink-0 text-muted-foreground/60" />
                        )}
                      </button>
                    </li>
                  );
                })}
              </ul>
            </>
          )}
        </div>
      )}
    </div>
  );
}
