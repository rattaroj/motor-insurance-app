'use client';

import Link from 'next/link';
import { toast } from 'sonner';
import {
  Users,
  Car,
  FileText,
  ShieldCheck,
  FileWarning,
  CreditCard,
  AlarmClock,
  CalendarClock,
  BellOff,
  type LucideIcon,
  LayoutDashboard,
} from 'lucide-react';
import {
  useGetDashboardSummaryQuery,
  useGetClaimsAgingQuery,
  useGetExpiringPoliciesQuery,
  useGetNotificationsQuery,
} from '@/lib/api/insuranceApi';
import { Card, CardContent } from '@/components/ui/card';
import { Skeleton } from '@/components/ui/skeleton';
import { PageHeader } from '@/components/page-header';
import { P, usePermissions, requiredPermission } from '@/lib/auth/permissions';
import { cn, fmtBaht } from '@/lib/utils';

/**
 * A Link that checks the target route's required permission first. When the user lacks it,
 * navigation is blocked and an alert explains why (instead of the AuthProvider silently
 * bouncing them back to the dashboard).
 */
function GuardedLink({
  href,
  className,
  children,
}: {
  href: string;
  className?: string;
  children: React.ReactNode;
}) {
  const permissions = usePermissions();
  const required = requiredPermission(href);
  const allowed = !required || permissions.includes(required);

  return (
    <Link
      href={href}
      aria-disabled={!allowed}
      onClick={(e) => {
        if (!allowed) {
          e.preventDefault();
          toast.error('คุณไม่มีสิทธิ์เข้าถึงหน้านี้', {
            description: 'กรุณาติดต่อผู้ดูแลระบบหากต้องการสิทธิ์เพิ่มเติม',
          });
        }
      }}
      className={cn(className, !allowed && 'cursor-not-allowed')}
    >
      {children}
    </Link>
  );
}

function StatCard({
  href,
  label,
  value,
  icon: Icon,
  accent,
  loading,
}: {
  href: string;
  label: string;
  value: string | number;
  icon: LucideIcon;
  accent: string;
  loading?: boolean;
}) {
  return (
    <GuardedLink href={href} className="group block">
      <Card className="relative overflow-hidden transition-all duration-200 group-hover:-translate-y-0.5 group-hover:border-primary/30 group-hover:shadow-lg">
        <div
          aria-hidden
          className="absolute inset-x-0 top-0 h-0.5 bg-gradient-to-r from-sidebar to-primary opacity-0 transition-opacity duration-200 group-hover:opacity-100"
        />
        <CardContent className="flex items-center gap-4 p-5">
          <div
            className={cn(
              'flex h-12 w-12 items-center justify-center rounded-xl transition-transform duration-200 group-hover:scale-110',
              accent,
            )}
          >
            <Icon className="h-6 w-6" />
          </div>
          <div>
            <p className="text-sm text-muted-foreground">{label}</p>
            {loading ? (
              <Skeleton className="mt-1 h-8 w-16" />
            ) : (
              <p className="text-2xl font-bold tabular-nums">{value}</p>
            )}
          </div>
        </CardContent>
      </Card>
    </GuardedLink>
  );
}

/** A compact "work to do" alert tile that links to the relevant worklist. */
function AlertCard({
  href,
  label,
  count,
  icon: Icon,
  tone,
  loading,
}: {
  href: string;
  label: string;
  count: number;
  icon: LucideIcon;
  tone: 'red' | 'amber' | 'slate';
  loading?: boolean;
}) {
  const toneClass = {
    red: 'border-red-500/30 bg-red-500/10 text-red-700 dark:text-red-400',
    amber: 'border-amber-500/30 bg-amber-500/10 text-amber-700 dark:text-amber-400',
    slate: 'border-slate-500/30 bg-slate-500/10 text-slate-600 dark:text-slate-400',
  }[count > 0 ? tone : 'slate'];

  return (
    <GuardedLink href={href} className="group block">
      <Card className={cn('border transition-all duration-200 group-hover:-translate-y-0.5 group-hover:shadow-md', toneClass)}>
        <CardContent className="flex items-center gap-3 p-4">
          <Icon className="h-5 w-5 shrink-0" />
          <div className="min-w-0">
            <p className="text-sm">{label}</p>
            {loading ? (
              <Skeleton className="mt-1 h-7 w-12" />
            ) : (
              <p className="text-xl font-semibold tabular-nums">{count}</p>
            )}
          </div>
        </CardContent>
      </Card>
    </GuardedLink>
  );
}

export default function DashboardPage() {
  const { data, isLoading } = useGetDashboardSummaryQuery();
  const perms = usePermissions();
  const canClaims = perms.includes(P.ClaimRead);
  const canRenew = perms.includes(P.PolicyRead);
  const canNotif = perms.includes(P.NotificationRead);
  const canPayments = perms.includes(P.PaymentRead);

  const { data: aging, isLoading: agingLoading } = useGetClaimsAgingQuery(undefined, { skip: !canClaims });
  const { data: expiring, isLoading: expiringLoading } = useGetExpiringPoliciesQuery(
    { days: 60 },
    { skip: !canRenew },
  );
  const { data: failedNotif, isLoading: notifLoading } = useGetNotificationsQuery(
    { status: 'Failed', pageSize: 1 },
    { skip: !canNotif },
  );

  const breachedClaims = (aging ?? []).filter((a) => a.breached).length;
  const expiringCount = expiring?.length ?? 0;
  const failedCount = failedNotif?.totalCount ?? 0;
  const overdueCount = data?.installmentsOverdue ?? 0;
  const showAlerts = canClaims || canRenew || canNotif || canPayments;

  const show = (n?: number) => (n ?? '—') as string | number;

  return (
    <div className="space-y-8">
      <PageHeader icon={LayoutDashboard} title="แดชบอร์ด" description="ภาพรวมระบบประกันรถยนต์" />

      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
        <StatCard href="/customers" label="ลูกค้า" value={show(data?.customers)} icon={Users} accent="bg-blue-500/10 text-blue-700 dark:text-blue-400" loading={isLoading} />
        <StatCard href="/vehicles" label="รถยนต์" value={show(data?.vehicles)} icon={Car} accent="bg-sky-500/10 text-sky-700 dark:text-sky-400" loading={isLoading} />
        <StatCard href="/quotations" label="ใบเสนอราคา" value={show(data?.quotations)} icon={FileText} accent="bg-indigo-500/10 text-indigo-700 dark:text-indigo-400" loading={isLoading} />
        <StatCard href="/policies" label="กรมธรรม์ทั้งหมด" value={show(data?.policiesTotal)} icon={ShieldCheck} accent="bg-blue-500/10 text-blue-700 dark:text-blue-400" loading={isLoading} />
        <StatCard href="/policies" label="คุ้มครองอยู่" value={show(data?.policiesActive)} icon={ShieldCheck} accent="bg-emerald-500/10 text-emerald-700 dark:text-emerald-400" loading={isLoading} />
        <StatCard href="/claims" label="เคลมที่ยังไม่ปิด" value={show(data?.claimsOpen)} icon={FileWarning} accent="bg-amber-500/10 text-amber-700 dark:text-amber-400" loading={isLoading} />
      </div>

      {showAlerts && (
        <div className="space-y-3">
          <h2 className="text-sm font-medium text-muted-foreground">งานค้างที่ต้องติดตาม</h2>
          <div className="grid gap-4 sm:grid-cols-3">
            {canClaims && (
              <AlertCard
                href="/claims"
                label="เคลมเกินกำหนด SLA"
                count={breachedClaims}
                icon={AlarmClock}
                tone="red"
                loading={agingLoading}
              />
            )}
            {canRenew && (
              <AlertCard
                href="/renewals"
                label="ใกล้หมดอายุ (60 วัน)"
                count={expiringCount}
                icon={CalendarClock}
                tone="amber"
                loading={expiringLoading}
              />
            )}
            {canPayments && (
              <AlertCard
                href="/payments/overdue"
                label="งวดผ่อนเกินกำหนด"
                count={overdueCount}
                icon={AlarmClock}
                tone="red"
                loading={isLoading}
              />
            )}
            {canNotif && (
              <AlertCard
                href="/notifications"
                label="แจ้งเตือนที่ส่งล้มเหลว"
                count={failedCount}
                icon={BellOff}
                tone="red"
                loading={notifLoading}
              />
            )}
          </div>
        </div>
      )}

      <Card>
        <CardContent className="flex flex-wrap items-center justify-between gap-4 p-5">
          <div className="flex items-center gap-4">
            <div className="flex h-12 w-12 items-center justify-center rounded-lg bg-amber-500/10 text-amber-700 dark:text-amber-400">
              <CreditCard className="h-6 w-6" />
            </div>
            <div>
              <p className="text-sm text-muted-foreground">รายการรอชำระ</p>
              {isLoading ? (
                <Skeleton className="mt-1 h-8 w-16" />
              ) : (
                <p className="text-2xl font-semibold tabular-nums">{show(data?.paymentsPending)}</p>
              )}
            </div>
          </div>
          <div className="text-right">
            <p className="text-sm text-muted-foreground">ยอดรวมรอชำระ</p>
            {isLoading ? (
              <Skeleton className="mt-1 ml-auto h-8 w-28" />
            ) : (
              <p className="text-2xl font-semibold tabular-nums text-blue-700 dark:text-blue-400">
                {data ? fmtBaht(data.paymentsPendingAmount) : '—'}
              </p>
            )}
          </div>
          <GuardedLink href="/payments" className="text-sm font-medium text-primary hover:underline">
            ไปที่การชำระเงิน →
          </GuardedLink>
        </CardContent>
      </Card>
    </div>
  );
}
