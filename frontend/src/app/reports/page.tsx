'use client';

import { useState } from 'react';
import {
  useGetAnalyticsQuery,
  useExportAnalyticsMutation,
  useGetConversionQuery,
  useExportConversionMutation,
  type LabelCount,
  type Conversion,
} from '@/lib/api/insuranceApi';
import { BarChart3 } from 'lucide-react';
import {
  Bar,
  BarChart,
  CartesianGrid,
  Legend,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Skeleton } from '@/components/ui/skeleton';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { PageHeader } from '@/components/page-header';
import { ExportButton } from '@/components/export-button';
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

/** Quote-to-bind conversion: KPIs, per-coverage funnel and the monthly quotes-vs-bound trend. */
function ConversionSection({ from, to }: { from: string; to: string }) {
  const { data, isFetching } = useGetConversionQuery({ from: from || undefined, to: to || undefined });
  const [exportConversion] = useExportConversionMutation();

  if (!data) return <Skeleton className="h-64" />;

  const c: Conversion = data;
  const monthly = c.byMonth.map((m) => ({ ...m, name: monthLabel(m.month) }));
  const maxCov = Math.max(1, ...c.byCoverage.map((x) => x.quotes));

  return (
    <Card className={cn(isFetching && 'opacity-60 transition-opacity')}>
      <CardHeader className="flex flex-row items-center justify-between space-y-0">
        <CardTitle className="text-base">อัตราปิดการขาย (ใบเสนอราคา → กรมธรรม์)</CardTitle>
        <ExportButton
          filename="conversion.csv"
          fetchUrl={() => exportConversion({ from: from || undefined, to: to || undefined }).unwrap()}
        />
      </CardHeader>
      <CardContent className="space-y-6">
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
          <Kpi
            label="อัตราปิดการขาย"
            value={`${(c.conversionRate * 100).toFixed(1)}%`}
            accent="text-emerald-600 dark:text-emerald-400"
          />
          <Kpi label="ใบเสนอราคา / ปิดได้" value={`${c.totalQuotes} / ${c.boundQuotes}`} />
          <Kpi
            label="รอตัดสินใจ / หมดอายุ"
            value={`${c.openQuotes} / ${c.expiredUnbound}`}
            accent="text-amber-700 dark:text-amber-400"
          />
          <Kpi label="เฉลี่ยวันจากเสนอถึงปิด" value={`${c.avgDaysToBind.toFixed(1)} วัน`} />
        </div>

        <div className="grid gap-4 sm:grid-cols-2">
          <Kpi label="เบี้ยที่เสนอรวม" value={fmtBaht(c.quotedPremium)} />
          <Kpi
            label="เบี้ยที่ปิดได้"
            value={fmtBaht(c.boundPremium)}
            accent="text-blue-700 dark:text-blue-400"
          />
        </div>

        <div>
          <p className="mb-3 text-sm font-medium text-muted-foreground">ตามชั้นความคุ้มครอง</p>
          {c.byCoverage.length === 0 ? (
            <p className="py-6 text-center text-sm text-muted-foreground">ไม่มีข้อมูล</p>
          ) : (
            <div className="space-y-2.5">
              {c.byCoverage.map((x) => (
                <div key={x.coverage} className="flex items-center gap-3 text-sm">
                  <span className="w-20 shrink-0 text-muted-foreground">{COVERAGE_TH[x.coverage] ?? x.coverage}</span>
                  <div className="relative h-5 flex-1 overflow-hidden rounded bg-muted">
                    <div className="h-full rounded bg-blue-200 dark:bg-blue-900" style={{ width: `${(x.quotes / maxCov) * 100}%` }} />
                    <div className="absolute inset-y-0 left-0 rounded bg-emerald-500" style={{ width: `${(x.bound / maxCov) * 100}%` }} />
                  </div>
                  <span className="w-24 shrink-0 text-right font-medium tabular-nums">
                    {x.bound}/{x.quotes} ({(x.rate * 100).toFixed(0)}%)
                  </span>
                </div>
              ))}
            </div>
          )}
        </div>

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
                width={32}
                allowDecimals={false}
              />
              <Tooltip
                cursor={{ fill: 'hsl(var(--accent))', opacity: 0.5 }}
                contentStyle={{
                  background: 'hsl(var(--popover))',
                  border: '1px solid hsl(var(--border))',
                  borderRadius: 8,
                  color: 'hsl(var(--popover-foreground))',
                  fontSize: 13,
                }}
              />
              <Legend wrapperStyle={{ fontSize: 12 }} />
              <Bar dataKey="quotes" name="ใบเสนอราคา" fill="hsl(var(--muted-foreground))" radius={[4, 4, 0, 0]} maxBarSize={28} />
              <Bar dataKey="bound" name="ปิดได้" fill="hsl(var(--primary))" radius={[4, 4, 0, 0]} maxBarSize={28} />
            </BarChart>
          </ResponsiveContainer>
        </div>
      </CardContent>
    </Card>
  );
}

export default function ReportsPage() {
  const [from, setFrom] = useState('');
  const [to, setTo] = useState('');
  const { data, isFetching } = useGetAnalyticsQuery({ from: from || undefined, to: to || undefined });
  const [exportAnalytics] = useExportAnalyticsMutation();

  const monthly = (data?.premiumByMonth ?? []).map((m) => ({ ...m, name: monthLabel(m.month) }));

  return (
    <div className="space-y-6">
      <PageHeader
        icon={BarChart3}
        title="รายงานและวิเคราะห์"
        description="ภาพรวมเบี้ยรับ ค่าสินไหม และพอร์ตกรมธรรม์"
        actions={
          <div className="flex flex-wrap items-end gap-2">
            <div className="space-y-1">
              <Label htmlFor="from" className="text-xs text-muted-foreground">ตั้งแต่</Label>
              <Input id="from" type="date" value={from} onChange={(e) => setFrom(e.target.value)} className="h-9 w-40" />
            </div>
            <div className="space-y-1">
              <Label htmlFor="to" className="text-xs text-muted-foreground">ถึง</Label>
              <Input id="to" type="date" value={to} onChange={(e) => setTo(e.target.value)} className="h-9 w-40" />
            </div>
            {(from || to) && (
              <button
                type="button"
                onClick={() => {
                  setFrom('');
                  setTo('');
                }}
                className="h-9 px-2 text-sm text-muted-foreground hover:text-foreground"
              >
                ล้าง
              </button>
            )}
            <ExportButton
              filename="analytics.csv"
              fetchUrl={() => exportAnalytics({ from: from || undefined, to: to || undefined }).unwrap()}
            />
          </div>
        }
      />

      {!data ? (
        <div className="space-y-6">
          <div className="grid gap-4 sm:grid-cols-3">
            {Array.from({ length: 3 }).map((_, i) => (
              <Skeleton key={i} className="h-24" />
            ))}
          </div>
          <Skeleton className="h-64" />
        </div>
      ) : (
        <div className={cn('space-y-6', isFetching && 'opacity-60 transition-opacity')}>
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

      <ConversionSection from={from} to={to} />
        </div>
      )}
    </div>
  );
}
