'use client';

import { useState } from 'react';
import { AlarmClock, ChevronDown } from 'lucide-react';
import { useGetClaimsAgingQuery } from '@/lib/api/insuranceApi';
import { Card, CardContent } from '@/components/ui/card';
import { Skeleton } from '@/components/ui/skeleton';
import { StatusBadge } from '@/components/StatusBadge';
import { cn, fmtBaht, fmtDate } from '@/lib/utils';

/**
 * Claims SLA worklist: open claims and how long they've sat in their current status, breached
 * (over-SLA) ones first. Collapsible summary that sits above the main claims table.
 */
export function ClaimsAgingPanel() {
  const { data, isLoading } = useGetClaimsAgingQuery();
  const [open, setOpen] = useState(true);

  const rows = data ?? [];
  const breachedCount = rows.filter((r) => r.breached).length;

  if (!isLoading && rows.length === 0) return null;

  return (
    <Card>
      <button
        type="button"
        onClick={() => setOpen((o) => !o)}
        className="flex w-full items-center justify-between px-6 py-4 text-left"
      >
        <span className="flex items-center gap-2 font-medium">
          <AlarmClock className="h-4 w-4 text-amber-600" />
          เคลมที่ต้องติดตาม (SLA)
          {breachedCount > 0 && (
            <span className="rounded-full bg-red-500/10 px-2 py-0.5 text-xs font-medium text-red-700 dark:text-red-400">
              เกินกำหนด {breachedCount}
            </span>
          )}
        </span>
        <ChevronDown className={cn('h-4 w-4 transition-transform', open && 'rotate-180')} />
      </button>

      {open && (
        <CardContent className="pt-0">
          {isLoading ? (
            <div className="space-y-2">
              {Array.from({ length: 3 }).map((_, i) => <Skeleton key={i} className="h-8 w-full" />)}
            </div>
          ) : (
            <div className="space-y-1.5">
              {rows.map((r) => (
                <div
                  key={r.id}
                  className={cn(
                    'flex flex-wrap items-center gap-x-4 gap-y-1 rounded-md border px-3 py-2 text-sm',
                    r.breached ? 'border-red-500/30 bg-red-500/10' : 'bg-muted/30',
                  )}
                >
                  <span className="font-medium">{r.claimNo}</span>
                  <span className="text-muted-foreground">{r.policyNo}</span>
                  <StatusBadge status={r.status} />
                  <span className="text-muted-foreground">ตั้งแต่ {fmtDate(r.statusSince)}</span>
                  <span className="ml-auto tabular-nums text-muted-foreground">{fmtBaht(r.claimedAmount)}</span>
                  <span
                    className={cn(
                      'inline-block rounded-full px-2 py-0.5 text-xs font-medium tabular-nums',
                      r.breached ? 'bg-red-500/15 text-red-700 dark:text-red-400' : 'bg-amber-500/15 text-amber-700 dark:text-amber-400',
                    )}
                    title={`SLA ${r.slaDays} วัน`}
                  >
                    {r.daysInStatus}/{r.slaDays} วัน
                  </span>
                </div>
              ))}
            </div>
          )}
        </CardContent>
      )}
    </Card>
  );
}
