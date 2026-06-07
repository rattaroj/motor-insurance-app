'use client';

import Link from 'next/link';
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
} from 'lucide-react';
import {
  useGetDashboardSummaryQuery,
  useGetClaimsAgingQuery,
  useGetExpiringPoliciesQuery,
  useGetNotificationsQuery,
} from '@/lib/api/insuranceApi';
import { Card, CardContent } from '@/components/ui/card';
import { Skeleton } from '@/components/ui/skeleton';
import { P, usePermissions } from '@/lib/auth/permissions';
import { cn, fmtBaht } from '@/lib/utils';

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
    <Link href={href}>
      <Card className="transition-shadow hover:shadow-md">
        <CardContent className="flex items-center gap-4 p-5">
          <div className={cn('flex h-12 w-12 items-center justify-center rounded-lg', accent)}>
            <Icon className="h-6 w-6" />
          </div>
          <div>
            <p className="text-sm text-muted-foreground">{label}</p>
            {loading ? (
              <Skeleton className="mt-1 h-8 w-16" />
            ) : (
              <p className="text-2xl font-semibold tabular-nums">{value}</p>
            )}
          </div>
        </CardContent>
      </Card>
    </Link>
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
    red: 'border-red-200 bg-red-50 text-red-700',
    amber: 'border-amber-200 bg-amber-50 text-amber-700',
    slate: 'border-slate-200 bg-slate-50 text-slate-600',
  }[count > 0 ? tone : 'slate'];

  return (
    <Link href={href}>
      <Card className={cn('border transition-shadow hover:shadow-md', toneClass)}>
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
    </Link>
  );
}

export default function DashboardPage() {
  const { data, isLoading } = useGetDashboardSummaryQuery();
  const perms = usePermissions();
  const canClaims = perms.includes(P.ClaimRead);
  const canRenew = perms.includes(P.PolicyRead);
  const canNotif = perms.includes(P.NotificationRead);

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
  const showAlerts = canClaims || canRenew || canNotif;

  const show = (n?: number) => (n ?? '—') as string | number;

  return (
    <div className="space-y-8">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">แดชบอร์ด</h1>
        <p className="text-sm text-muted-foreground">ภาพรวมระบบประกันรถยนต์</p>
      </div>

      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
        <StatCard href="/customers" label="ลูกค้า" value={show(data?.customers)} icon={Users} accent="bg-blue-100 text-blue-700" loading={isLoading} />
        <StatCard href="/vehicles" label="รถยนต์" value={show(data?.vehicles)} icon={Car} accent="bg-sky-100 text-sky-700" loading={isLoading} />
        <StatCard href="/quotations" label="ใบเสนอราคา" value={show(data?.quotations)} icon={FileText} accent="bg-indigo-100 text-indigo-700" loading={isLoading} />
        <StatCard href="/policies" label="กรมธรรม์ทั้งหมด" value={show(data?.policiesTotal)} icon={ShieldCheck} accent="bg-blue-100 text-blue-700" loading={isLoading} />
        <StatCard href="/policies" label="คุ้มครองอยู่" value={show(data?.policiesActive)} icon={ShieldCheck} accent="bg-emerald-100 text-emerald-700" loading={isLoading} />
        <StatCard href="/claims" label="เคลมที่ยังไม่ปิด" value={show(data?.claimsOpen)} icon={FileWarning} accent="bg-amber-100 text-amber-700" loading={isLoading} />
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
            <div className="flex h-12 w-12 items-center justify-center rounded-lg bg-amber-100 text-amber-700">
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
              <p className="text-2xl font-semibold tabular-nums text-blue-700">
                {data ? fmtBaht(data.paymentsPendingAmount) : '—'}
              </p>
            )}
          </div>
          <Link href="/payments" className="text-sm font-medium text-primary hover:underline">
            ไปที่การชำระเงิน →
          </Link>
        </CardContent>
      </Card>
    </div>
  );
}
