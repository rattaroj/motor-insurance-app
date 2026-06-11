'use client';

import { useState } from 'react';
import Link from 'next/link';
import { toast } from 'sonner';
import { AlarmClock, BellRing, CheckCircle2 } from 'lucide-react';
import {
  useGetOverdueInstallmentsQuery,
  useSendInstallmentReminderMutation,
  type OverdueInstallment,
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
import { PageHeader } from '@/components/page-header';
import { P } from '@/lib/auth/permissions';
import { apiError, cn, fmtBaht, fmtDate, fmtDateTime } from '@/lib/utils';

export default function OverdueInstallmentsPage() {
  const { data, isLoading } = useGetOverdueInstallmentsQuery();
  const [remind, { isLoading: reminding }] = useSendInstallmentReminderMutation();
  const [confirm, setConfirm] = useState<OverdueInstallment | null>(null);

  const rows = data ?? [];
  const totalOutstanding = rows.reduce((sum, r) => sum + r.amount, 0);

  const doRemind = async () => {
    if (!confirm) return;
    try {
      const res = await remind(confirm.paymentId).unwrap();
      toast.success('บันทึกการแจ้งเตือนแล้ว', { description: `${res.channel} → ${res.recipient}` });
      setConfirm(null);
    } catch (e) {
      toast.error(apiError(e));
    }
  };

  return (
    <div className="space-y-6">
      <PageHeader
        icon={AlarmClock}
        title="งวดผ่อนเกินกำหนด"
        description="งวดเบี้ยที่ครบกำหนดชำระแล้วแต่ยังไม่ได้รับชำระ — ติดตามก่อนกรมธรรม์ถูกระงับ"
      />

      {rows.length > 0 && (
        <Card className="border-red-500/30 bg-red-500/10">
          <CardContent className="flex flex-wrap items-center justify-between gap-2 p-4 text-sm">
            <span className="font-medium text-red-700 dark:text-red-400">
              ค้างชำระ {rows.length} งวด
            </span>
            <span className="tabular-nums text-red-700 dark:text-red-400">
              ยอดรวม {fmtBaht(totalOutstanding)}
            </span>
          </CardContent>
        </Card>
      )}

      <Card>
        <CardContent className="pt-6">
          {isLoading ? (
            <div className="space-y-3">
              {Array.from({ length: 3 }).map((_, i) => (
                <Skeleton key={i} className="h-10 w-full" />
              ))}
            </div>
          ) : (
            <Table>
              <TableHeader>
                <TableRow className="bg-muted/50 hover:bg-muted/50">
                  <TableHead>กรมธรรม์</TableHead>
                  <TableHead>ลูกค้า</TableHead>
                  <TableHead>ช่องทางติดต่อ</TableHead>
                  <TableHead className="text-center">งวดที่</TableHead>
                  <TableHead className="text-right">จำนวน</TableHead>
                  <TableHead>ครบกำหนด</TableHead>
                  <TableHead className="text-center">เกินกำหนด</TableHead>
                  <TableHead>แจ้งเตือนล่าสุด</TableHead>
                  <TableHead className="text-right">จัดการ</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {rows.map((r) => (
                  <TableRow key={r.paymentId}>
                    <TableCell>
                      <Link href={`/policies/${r.policyId}`} className="font-medium text-primary hover:underline">
                        {r.policyNo}
                      </Link>
                    </TableCell>
                    <TableCell>{r.customerName}</TableCell>
                    <TableCell className="text-muted-foreground">{r.customerEmail ?? r.customerPhone ?? '-'}</TableCell>
                    <TableCell className="text-center tabular-nums">{r.installmentSeq}</TableCell>
                    <TableCell className="text-right tabular-nums">{fmtBaht(r.amount)}</TableCell>
                    <TableCell>{fmtDate(r.dueDate)}</TableCell>
                    <TableCell className="text-center">
                      <span
                        className={cn(
                          'inline-block rounded-full px-2 py-0.5 text-xs font-medium tabular-nums',
                          r.daysOverdue >= 14
                            ? 'bg-red-500/15 text-red-700 dark:text-red-400'
                            : 'bg-amber-500/15 text-amber-700 dark:text-amber-400',
                        )}
                      >
                        {r.daysOverdue} วัน
                      </span>
                    </TableCell>
                    <TableCell className="text-muted-foreground">
                      {r.lastRemindedAt ? (
                        <span className="inline-flex items-center gap-1 text-emerald-600 dark:text-emerald-400">
                          <CheckCircle2 className="h-3.5 w-3.5" /> {fmtDateTime(r.lastRemindedAt)}
                        </span>
                      ) : (
                        '—'
                      )}
                    </TableCell>
                    <TableCell className="text-right">
                      <Can permission={P.PaymentRead}>
                        <Button size="sm" variant="ghost" disabled={reminding} onClick={() => setConfirm(r)}>
                          <BellRing /> แจ้งเตือน
                        </Button>
                      </Can>
                    </TableCell>
                  </TableRow>
                ))}
                {rows.length === 0 && (
                  <TableRow>
                    <TableCell colSpan={9} className="py-8 text-center text-muted-foreground">
                      ไม่มีงวดผ่อนที่เกินกำหนด
                    </TableCell>
                  </TableRow>
                )}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>

      {/* Confirm before sending — reminder goes out over email/SMS/LINE. */}
      <Dialog open={confirm !== null} onOpenChange={(open) => !open && setConfirm(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>ยืนยันการส่งแจ้งเตือน</DialogTitle>
            <DialogDescription>
              {confirm
                ? `ส่งแจ้งเตือนชำระเบี้ยงวดที่ ${confirm.installmentSeq} กรมธรรม์ ${confirm.policyNo} ไปยัง ${confirm.customerEmail ?? confirm.customerPhone ?? '-'}`
                : ''}
            </DialogDescription>
          </DialogHeader>
          <DialogFooter>
            <Button variant="outline" onClick={() => setConfirm(null)}>
              ยกเลิก
            </Button>
            <Button disabled={reminding} onClick={doRemind}>
              <BellRing /> ยืนยันส่ง
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
