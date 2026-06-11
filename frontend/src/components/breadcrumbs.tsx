'use client';

import Link from 'next/link';
import { usePathname } from 'next/navigation';
import { ChevronRight } from 'lucide-react';

/** Thai labels per path segment (mirrors the sidebar nav). */
const SEGMENT_LABEL: Record<string, string> = {
  customers: 'ลูกค้า',
  vehicles: 'รถยนต์',
  quotations: 'ใบเสนอราคา',
  policies: 'กรมธรรม์',
  renewals: 'ต่ออายุ',
  claims: 'เคลม',
  payments: 'การชำระเงิน',
  overdue: 'งวดผ่อนเกินกำหนด',
  notifications: 'การแจ้งเตือน',
  reports: 'รายงาน',
  admin: 'ผู้ดูแลระบบ',
  users: 'ผู้ใช้งานระบบ',
  master: 'ข้อมูลหลัก',
  titles: 'คำนำหน้าชื่อ',
  riders: 'ความคุ้มครองเสริม',
  garages: 'อู่/ศูนย์ซ่อม',
  rates: 'พิกัดอัตราเบี้ย',
  new: 'เพิ่มใหม่',
  edit: 'แก้ไข',
};

// Segments that are pure grouping with no landing page of their own.
const NO_PAGE = new Set(['admin']);

function label(segment: string): string {
  if (SEGMENT_LABEL[segment]) return SEGMENT_LABEL[segment];
  if (/^\d+$/.test(segment)) return 'รายละเอียด';
  return decodeURIComponent(segment);
}

/** Header breadcrumb derived from the current route, e.g. กรมธรรม์ › รายละเอียด. */
export function Breadcrumbs() {
  const pathname = usePathname();
  const segments = pathname.split('/').filter(Boolean);

  if (segments.length === 0) {
    return <span className="hidden truncate font-semibold md:inline">แดชบอร์ด</span>;
  }

  const crumbs = segments.map((seg, i) => ({
    label: label(seg),
    href: '/' + segments.slice(0, i + 1).join('/'),
    last: i === segments.length - 1,
    linkable: !NO_PAGE.has(seg),
  }));

  return (
    <nav aria-label="breadcrumb" className="hidden min-w-0 items-center gap-1 text-sm md:flex">
      <Link href="/" className="shrink-0 text-muted-foreground transition-colors hover:text-foreground">
        แดชบอร์ด
      </Link>
      {crumbs.map((c) => (
        <span key={c.href} className="flex min-w-0 items-center gap-1">
          <ChevronRight className="h-3.5 w-3.5 shrink-0 text-muted-foreground/50" />
          {c.last || !c.linkable ? (
            <span className={c.last ? 'truncate font-semibold' : 'truncate text-muted-foreground'}>{c.label}</span>
          ) : (
            <Link href={c.href} className="truncate text-muted-foreground transition-colors hover:text-foreground">
              {c.label}
            </Link>
          )}
        </span>
      ))}
    </nav>
  );
}
