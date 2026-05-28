'use client';

import Link from 'next/link';
import { Users, Car, FileText, ShieldCheck, FileWarning, CreditCard, type LucideIcon } from 'lucide-react';
import { useGetDashboardSummaryQuery } from '@/lib/api/insuranceApi';
import { Card, CardContent } from '@/components/ui/card';
import { cn, fmtBaht } from '@/lib/utils';

function StatCard({
  href,
  label,
  value,
  icon: Icon,
  accent,
}: {
  href: string;
  label: string;
  value: string | number;
  icon: LucideIcon;
  accent: string;
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
            <p className="text-2xl font-semibold tabular-nums">{value}</p>
          </div>
        </CardContent>
      </Card>
    </Link>
  );
}

export default function DashboardPage() {
  const { data } = useGetDashboardSummaryQuery();
  const show = (n?: number) => (n ?? '—') as string | number;

  return (
    <div className="space-y-8">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">แดชบอร์ด</h1>
        <p className="text-sm text-muted-foreground">ภาพรวมระบบประกันรถยนต์</p>
      </div>

      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
        <StatCard href="/customers" label="ลูกค้า" value={show(data?.customers)} icon={Users} accent="bg-blue-100 text-blue-700" />
        <StatCard href="/vehicles" label="รถยนต์" value={show(data?.vehicles)} icon={Car} accent="bg-sky-100 text-sky-700" />
        <StatCard href="/quotations" label="ใบเสนอราคา" value={show(data?.quotations)} icon={FileText} accent="bg-indigo-100 text-indigo-700" />
        <StatCard href="/policies" label="กรมธรรม์ทั้งหมด" value={show(data?.policiesTotal)} icon={ShieldCheck} accent="bg-blue-100 text-blue-700" />
        <StatCard href="/policies" label="คุ้มครองอยู่" value={show(data?.policiesActive)} icon={ShieldCheck} accent="bg-emerald-100 text-emerald-700" />
        <StatCard href="/claims" label="เคลมที่ยังไม่ปิด" value={show(data?.claimsOpen)} icon={FileWarning} accent="bg-amber-100 text-amber-700" />
      </div>

      <Card>
        <CardContent className="flex flex-wrap items-center justify-between gap-4 p-5">
          <div className="flex items-center gap-4">
            <div className="flex h-12 w-12 items-center justify-center rounded-lg bg-amber-100 text-amber-700">
              <CreditCard className="h-6 w-6" />
            </div>
            <div>
              <p className="text-sm text-muted-foreground">รายการรอชำระ</p>
              <p className="text-2xl font-semibold tabular-nums">{show(data?.paymentsPending)}</p>
            </div>
          </div>
          <div className="text-right">
            <p className="text-sm text-muted-foreground">ยอดรวมรอชำระ</p>
            <p className="text-2xl font-semibold tabular-nums text-blue-700">
              {data ? fmtBaht(data.paymentsPendingAmount) : '—'}
            </p>
          </div>
          <Link href="/payments" className="text-sm font-medium text-primary hover:underline">
            ไปที่การชำระเงิน →
          </Link>
        </CardContent>
      </Card>
    </div>
  );
}
