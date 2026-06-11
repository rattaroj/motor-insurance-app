'use client';

import { useGetAnalyticsQuery, type LabelCount } from '@/lib/api/insuranceApi';
import { BarChart3 } from 'lucide-react';
import {
  Bar,
  BarChart,
  CartesianGrid,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Skeleton } from '@/components/ui/skeleton';
import { PageHeader } from '@/components/page-header';
import { cn, fmtBaht } from '@/lib/utils';

const POLICY_STATUS_TH: Record<string, string> = {
  Draft: 'ฉบับร่าง', Quoted: 'เสนอราคาแล้ว', Issued: 'ออกกรมธรรม์แล้ว',
  Active: 'คุ้มครองอยู่', Cancelled: 'ยกเลิก', Expired: 'หมดอายุ',
};
const COVERAGE_TH: Record<string, string> = {
  Type1: 'ชั้น 1', Type2Plus: 'ชั้น 2+', Type3Plus: 'ชั้น 3+', Type3: 'ชั้น 3',
};
const CLAIM_STATUS_TH: Record<string, string> = {
  Filed: 'แจ้งเคลม', UnderReview: 'กำลังตรวจสอบ', Assessment: 'ประเมินความเสียหาย',
  Approved: 'อนุมัติ', Rejected: 'ปฏิเสธ', Paid: 'จ่ายแล้ว', Closed: 'ปิดเคลม',
};
const TH_MONTHS = ['ม.ค.', 'ก.พ.', 'มี.ค.', 'เม.ย.', 'พ.ค.', 'มิ.ย.', 'ก.ค.', 'ส.ค.', 'ก.ย.', 'ต.ค.', 'พ.ย.', 'ธ.ค.'];

/** "yyyy-MM" → Thai short month + Buddhist 2-digit year, e.g. "มิ.ย. 69". */
function monthLabel(ym: string) {
  const [y, m] = ym.split('-').map(Number);
  return `${TH_MONTHS[m - 1]} ${String((y + 543) % 100).padStart(2, '0')}`;
}

function Kpi({ label, value, accent }: { label: string; value: string; accent?: string }) {
  return (
    <Card>
      <CardContent className="p-5">
        <p className="text-sm text-muted-foreground">{label}</p>
        <p className={cn('mt-1 text-2xl font-semibold tabular-nums', accent)}>{value}</p>
      </CardContent>
    </Card>
  );
}

/** Horizontal bar list (counts) with Thai labels. */
function BarList({ items, labels, color }: { items: LabelCount[]; labels: Record<string, string>; color: string }) {
  const max = Math.max(1, ...items.map((i) => i.count));
  if (items.length === 0) return <p className="py-6 text-center text-sm text-muted-foreground">ไม่มีข้อมูล</p>;
  return (
    <div className="space-y-2.5">
      {items.map((it) => (
        <div key={it.label} className="flex items-center gap-3 text-sm">
          <span className="w-28 shrink-0 text-muted-foreground">{labels[it.label] ?? it.label}</span>
          <div className="h-5 flex-1 overflow-hidden rounded bg-muted">
            <div className={cn('h-full rounded', color)} style={{ width: `${(it.count / max) * 100}%` }} />
          </div>
          <span className="w-8 shrink-0 text-right font-medium tabular-nums">{it.count}</span>
        </div>
      ))}
    </div>
  );
}

export default function ReportsPage() {
  const { data, isLoading } = useGetAnalyticsQuery();

  if (isLoading || !data) {
    return (
      <div className="space-y-6">
        <Skeleton className="h-8 w-40" />
        <div className="grid gap-4 sm:grid-cols-3">
          {Array.from({ length: 3 }).map((_, i) => <Skeleton key={i} className="h-24" />)}
        </div>
        <Skeleton className="h-64" />
      </div>
    );
  }

  const monthly = data.premiumByMonth.map((m) => ({ ...m, name: monthLabel(m.month) }));

  return (
    <div className="space-y-6">
      <PageHeader icon={BarChart3} title="รายงานและวิเคราะห์" description="ภาพรวมเบี้ยรับ ค่าสินไหม และพอร์ตกรมธรรม์" />

      <div className="grid gap-4 sm:grid-cols-3">
        <Kpi label="เบี้ยรับรวม" value={fmtBaht(data.premiumWritten)} accent="text-blue-700 dark:text-blue-400" />
        <Kpi label="ค่าสินไหมจ่าย" value={fmtBaht(data.claimsPaid)} accent="text-amber-700 dark:text-amber-400" />
        <Kpi
          label="Loss Ratio (สินไหม/เบี้ย)"
          value={`${(data.lossRatio * 100).toFixed(1)}%`}
          accent={data.lossRatio > 0.7 ? 'text-red-600 dark:text-red-400' : 'text-emerald-600 dark:text-emerald-400'}
        />
      </div>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">เบี้ยรับรายเดือน (12 เดือนล่าสุด)</CardTitle>
        </CardHeader>
        <CardContent>
          <div className="h-64">
            <ResponsiveContainer width="100%" height="100%">
              <BarChart data={monthly} margin={{ top: 8, right: 8, left: 8, bottom: 0 }}>
                <CartesianGrid strokeDasharray="3 3" stroke="hsl(var(--border))" vertical={false} />
                <XAxis
                  dataKey="name"
                  tick={{ fontSize: 11, fill: 'hsl(var(--muted-foreground))' }}
                  tickLine={false}
                  axisLine={{ stroke: 'hsl(var(--border))' }}
                />
                <YAxis
                  tick={{ fontSize: 11, fill: 'hsl(var(--muted-foreground))' }}
                  tickLine={false}
                  axisLine={false}
                  width={70}
                  tickFormatter={(v: number) => (v >= 1000 ? `${(v / 1000).toLocaleString()}K` : String(v))}
                />
                <Tooltip
                  formatter={(value) => [fmtBaht(Number(value)), 'เบี้ยรับ']}
                  cursor={{ fill: 'hsl(var(--accent))', opacity: 0.5 }}
                  contentStyle={{
                    background: 'hsl(var(--popover))',
                    border: '1px solid hsl(var(--border))',
                    borderRadius: 8,
                    color: 'hsl(var(--popover-foreground))',
                    fontSize: 13,
                  }}
                />
                <Bar dataKey="premium" fill="hsl(var(--primary))" radius={[4, 4, 0, 0]} maxBarSize={48} />
              </BarChart>
            </ResponsiveContainer>
          </div>
        </CardContent>
      </Card>

      <div className="grid gap-4 lg:grid-cols-3">
        <Card>
          <CardHeader><CardTitle className="text-base">กรมธรรม์ตามสถานะ</CardTitle></CardHeader>
          <CardContent><BarList items={data.policiesByStatus} labels={POLICY_STATUS_TH} color="bg-blue-500" /></CardContent>
        </Card>
        <Card>
          <CardHeader><CardTitle className="text-base">กรมธรรม์ตามชั้น</CardTitle></CardHeader>
          <CardContent><BarList items={data.policiesByCoverage} labels={COVERAGE_TH} color="bg-indigo-500" /></CardContent>
        </Card>
        <Card>
          <CardHeader><CardTitle className="text-base">เคลมตามสถานะ</CardTitle></CardHeader>
          <CardContent><BarList items={data.claimsByStatus} labels={CLAIM_STATUS_TH} color="bg-amber-500" /></CardContent>
        </Card>
      </div>
    </div>
  );
}
