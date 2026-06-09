'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { toast } from 'sonner';
import { RefreshCw, BellRing, CheckCircle2 } from 'lucide-react';
import {
  useGetExpiringPoliciesQuery,
  useRenewPolicyMutation,
  useSendRenewalReminderMutation,
  useSendBulkRemindersMutation,
  useExportExpiringMutation,
} from '@/lib/api/insuranceApi';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from '@/components/ui/table';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogDescription,
  DialogFooter,
} from '@/components/ui/dialog';
import { Skeleton } from '@/components/ui/skeleton';
import { Can } from '@/components/can';
import { ExportButton } from '@/components/export-button';
import { P } from '@/lib/auth/permissions';
import { apiError, cn, fmtDate, fmtDateTime } from '@/lib/utils';

const WINDOW_DAYS = 60;

export default function RenewalsPage() {
  const router = useRouter();
  const { data, isLoading } = useGetExpiringPoliciesQuery({ days: WINDOW_DAYS });
  const [renew, { isLoading: renewing }] = useRenewPolicyMutation();
  const [remind, { isLoading: reminding }] = useSendRenewalReminderMutation();
  const [remindBulk, { isLoading: bulkReminding }] = useSendBulkRemindersMutation();
  const [exportExpiring] = useExportExpiringMutation();

  // Selected policy ids for the bulk-remind action.
  const [selected, setSelected] = useState<Set<number>>(new Set());
  // Pending send action awaiting confirmation (null = dialog closed).
  const [confirm, setConfirm] = useState<
    | { kind: 'single'; policyId: number; policyNo: string; contact: string }
    | { kind: 'bulk'; count: number }
    | null
  >(null);
  const rows = data ?? [];
  const allSelected = rows.length > 0 && selected.size === rows.length;

  const toggle = (id: number) =>
    setSelected((prev) => {
      const next = new Set(prev);
      next.has(id) ? next.delete(id) : next.add(id);
      return next;
    });
  const toggleAll = () => setSelected(allSelected ? new Set() : new Set(rows.map((r) => r.policyId)));

  const doRenew = async (policyId: number) => {
    try {
      const res = await renew({ policyId }).unwrap();
      toast.success('สร้างกรมธรรม์ต่ออายุแล้ว');
      router.push(`/policies/${res.id}`);
    } catch (e) {
      toast.error(apiError(e));
    }
  };

  const doRemind = async (policyId: number) => {
    try {
      const res = await remind(policyId).unwrap();
      toast.success('บันทึกการแจ้งเตือนแล้ว', { description: `${res.channel} → ${res.recipient}` });
    } catch (e) {
      toast.error(apiError(e));
    }
  };

  const doBulkRemind = async () => {
    try {
      const res = await remindBulk({ policyIds: [...selected] }).unwrap();
      toast.success(`ส่งแจ้งเตือนแล้ว ${res.sent}/${res.requested} รายการ`, {
        description: res.failed > 0 ? `ไม่สำเร็จ ${res.failed} รายการ` : undefined,
      });
      setSelected(new Set());
    } catch (e) {
      toast.error(apiError(e));
    }
  };

  // Run the action the confirm dialog is holding, then close it.
  const runConfirmed = async () => {
    if (!confirm) return;
    if (confirm.kind === 'single') await doRemind(confirm.policyId);
    else await doBulkRemind();
    setConfirm(null);
  };

  return (
    <div className="space-y-6">
      <div className="flex items-start justify-between">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">ต่ออายุเชิงรุก</h1>
          <p className="text-sm text-muted-foreground">
            กรมธรรม์ที่ใกล้หมดอายุภายใน {WINDOW_DAYS} วัน และยังไม่ได้ต่ออายุ
          </p>
        </div>
        <ExportButton filename="renewals-expiring.csv" fetchUrl={() => exportExpiring({ days: WINDOW_DAYS }).unwrap()} />
      </div>

      {/* Bulk action bar — only when at least one row is selected. */}
      {selected.size > 0 && (
        <Can permission={P.PolicyRenew}>
          <div className="flex items-center justify-between rounded-lg border bg-muted/40 px-4 py-2 text-sm">
            <span>เลือก {selected.size} รายการ</span>
            <div className="flex gap-2">
              <Button size="sm" variant="ghost" onClick={() => setSelected(new Set())}>
                ล้าง
              </Button>
              <Button
                size="sm"
                disabled={bulkReminding}
                onClick={() => setConfirm({ kind: 'bulk', count: selected.size })}
              >
                <BellRing /> ส่งเตือนที่เลือก
              </Button>
            </div>
          </div>
        </Can>
      )}

      <Card>
        <CardContent className="pt-6">
          {isLoading ? (
            <div className="space-y-3">
              {Array.from({ length: 3 }).map((_, i) => <Skeleton key={i} className="h-10 w-full" />)}
            </div>
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead className="w-10">
                    <input
                      type="checkbox"
                      aria-label="เลือกทั้งหมด"
                      className="h-4 w-4 cursor-pointer accent-primary"
                      checked={allSelected}
                      onChange={toggleAll}
                      disabled={rows.length === 0}
                    />
                  </TableHead>
                  <TableHead>กรมธรรม์</TableHead>
                  <TableHead>ลูกค้า</TableHead>
                  <TableHead>ช่องทางติดต่อ</TableHead>
                  <TableHead>หมดอายุ</TableHead>
                  <TableHead className="text-center">เหลือ (วัน)</TableHead>
                  <TableHead>แจ้งเตือนล่าสุด</TableHead>
                  <TableHead className="text-right">จัดการ</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {rows.map((r) => (
                  <TableRow key={r.policyId} data-state={selected.has(r.policyId) ? 'selected' : undefined}>
                    <TableCell>
                      <input
                        type="checkbox"
                        aria-label={`เลือก ${r.policyNo}`}
                        className="h-4 w-4 cursor-pointer accent-primary"
                        checked={selected.has(r.policyId)}
                        onChange={() => toggle(r.policyId)}
                      />
                    </TableCell>
                    <TableCell className="font-medium">{r.policyNo}</TableCell>
                    <TableCell>{r.customerName}</TableCell>
                    <TableCell className="text-muted-foreground">{r.customerEmail ?? r.customerPhone ?? '-'}</TableCell>
                    <TableCell>{fmtDate(r.expiryDate)}</TableCell>
                    <TableCell className="text-center">
                      <span
                        className={cn(
                          'inline-block rounded-full px-2 py-0.5 text-xs font-medium tabular-nums',
                          r.daysLeft <= 14 ? 'bg-red-100 text-red-700' : 'bg-amber-100 text-amber-700',
                        )}
                      >
                        {r.daysLeft}
                      </span>
                    </TableCell>
                    <TableCell className="text-muted-foreground">
                      {r.lastRemindedAt ? (
                        <span className="inline-flex items-center gap-1 text-emerald-600">
                          <CheckCircle2 className="h-3.5 w-3.5" /> {fmtDateTime(r.lastRemindedAt)}
                        </span>
                      ) : (
                        '—'
                      )}
                    </TableCell>
                    <TableCell className="text-right">
                      <div className="flex justify-end gap-2">
                        <Can permission={P.PolicyRenew}>
                          <Button
                            size="sm"
                            variant="ghost"
                            disabled={reminding}
                            onClick={() =>
                              setConfirm({
                                kind: 'single',
                                policyId: r.policyId,
                                policyNo: r.policyNo,
                                contact: r.customerEmail ?? r.customerPhone ?? '-',
                              })
                            }
                          >
                            <BellRing /> แจ้งเตือน
                          </Button>
                        </Can>
                        <Can permission={P.PolicyRenew}>
                          <Button size="sm" variant="outline" disabled={renewing} onClick={() => doRenew(r.policyId)}>
                            <RefreshCw /> ต่ออายุ
                          </Button>
                        </Can>
                      </div>
                    </TableCell>
                  </TableRow>
                ))}
                {rows.length === 0 && (
                  <TableRow>
                    <TableCell colSpan={8} className="py-8 text-center text-muted-foreground">
                      ไม่มีกรมธรรม์ที่ใกล้หมดอายุ
                    </TableCell>
                  </TableRow>
                )}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>

      {/* Confirm before sending — reminders go out over email/SMS/LINE. */}
      <Dialog open={confirm !== null} onOpenChange={(open) => !open && setConfirm(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>ยืนยันการส่งแจ้งเตือน</DialogTitle>
            <DialogDescription>
              {confirm?.kind === 'single'
                ? `ส่งแจ้งเตือนต่ออายุกรมธรรม์ ${confirm.policyNo} ไปยัง ${confirm.contact}`
                : confirm?.kind === 'bulk'
                  ? `ส่งแจ้งเตือนต่ออายุไปยังลูกค้าที่เลือก ${confirm.count} รายการ`
                  : ''}
            </DialogDescription>
          </DialogHeader>
          <DialogFooter>
            <Button variant="outline" onClick={() => setConfirm(null)}>
              ยกเลิก
            </Button>
            <Button disabled={reminding || bulkReminding} onClick={runConfirmed}>
              <BellRing /> ยืนยันส่ง
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
