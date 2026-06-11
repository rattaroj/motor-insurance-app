'use client';

import { Suspense, useState } from 'react';
import { toast } from 'sonner';
import Link from 'next/link';
import { Wallet, Receipt, CreditCard, AlarmClock } from 'lucide-react';
import {
  useGetPaymentsQuery,
  useSettlePaymentMutation,
  useGetPaymentReceiptMutation,
  useExportPaymentsMutation,
  type PaymentDto,
} from '@/lib/api/insuranceApi';
import { StatusBadge } from '@/components/StatusBadge';
import { ExportButton } from '@/components/export-button';
import { DataTable } from '@/components/data-table';
import { Button } from '@/components/ui/button';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogDescription,
  DialogFooter,
} from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Can } from '@/components/can';
import { PromptPayButton } from '@/components/promptpay-button';
import { PageHeader } from '@/components/page-header';
import { SavedViews } from '@/components/saved-views';
import { P } from '@/lib/auth/permissions';
import { apiError, fmtBaht, fmtDate, fmtDateTime, saveUrl } from '@/lib/utils';
import { useListUrlState } from '@/lib/use-url-state';

const PAGE_SIZE = 10;

const PAYMENT_STATUSES: { value: string; label: string }[] = [
  { value: 'all', label: 'ทุกสถานะ' },
  { value: 'Pending', label: 'รอชำระ' },
  { value: 'Paid', label: 'ชำระแล้ว' },
  { value: 'Failed', label: 'ไม่สำเร็จ' },
  { value: 'Refunded', label: 'คืนเงินแล้ว' },
];

const DIRECTIONS: { value: string; label: string }[] = [
  { value: 'all', label: 'ทุกทิศทาง' },
  { value: 'Inbound', label: 'รับเบี้ย' },
  { value: 'Outbound', label: 'จ่ายสินไหม' },
];

function PaymentsPageContent() {
  const { page, setPage, searchInput, onSearchChange, search, filters, setFilter } = useListUrlState([
    'status',
    'direction',
  ]);
  const statusFilter = filters.status;
  const directionFilter = filters.direction;
  const { data, isFetching } = useGetPaymentsQuery({
    page,
    pageSize: PAGE_SIZE,
    search,
    status: statusFilter === 'all' ? undefined : statusFilter,
    direction: directionFilter === 'all' ? undefined : directionFilter,
  });
  const [settle, { isLoading: settling }] = useSettlePaymentMutation();
  const [getReceipt] = useGetPaymentReceiptMutation();
  const [exportPayments] = useExportPaymentsMutation();
  const [settleFor, setSettleFor] = useState<PaymentDto | null>(null);
  const [referenceNo, setReferenceNo] = useState('');

  const downloadReceipt = async (paymentId: number, paymentNo: string) => {
    try {
      const url = await getReceipt(paymentId).unwrap();
      saveUrl(url, `${paymentNo}.pdf`);
    } catch (e) {
      toast.error(apiError(e));
    }
  };

  const submit = async () => {
    if (!settleFor) return;
    try {
      await settle({ id: settleFor.id, referenceNo }).unwrap();
      toast.success('บันทึกการชำระแล้ว');
      setSettleFor(null);
      setReferenceNo('');
    } catch (e) {
      toast.error(apiError(e));
    }
  };

  return (
    <div className="space-y-6">
      <PageHeader
        icon={CreditCard}
        title="การชำระเงิน"
        description="เบี้ยรับเข้า และสินไหมจ่ายออก"
        actions={
          <Button variant="outline" asChild>
            <Link href="/payments/overdue">
              <AlarmClock /> งวดผ่อนเกินกำหนด
            </Link>
          </Button>
        }
      />

      <DataTable<PaymentDto>
        rows={data?.items}
        loading={isFetching}
        getKey={(p) => p.id}
        page={page}
        pageSize={PAGE_SIZE}
        totalCount={data?.totalCount ?? 0}
        onPageChange={setPage}
        search={searchInput}
        onSearchChange={onSearchChange}
        searchPlaceholder="ค้นหาเลขที่ / อ้างอิง"
        emptyText="ไม่มีรายการชำระเงิน"
        toolbar={
          <>
            <Select
              value={statusFilter}
              onValueChange={(v) => setFilter('status', v)}
            >
              <SelectTrigger className="w-36">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {PAYMENT_STATUSES.map((s) => (
                  <SelectItem key={s.value} value={s.value}>
                    {s.label}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
            <Select
              value={directionFilter}
              onValueChange={(v) => setFilter('direction', v)}
            >
              <SelectTrigger className="w-36">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {DIRECTIONS.map((d) => (
                  <SelectItem key={d.value} value={d.value}>
                    {d.label}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
            <ExportButton
              filename="payments.csv"
              fetchUrl={() =>
                exportPayments({
                  search: search || undefined,
                  status: statusFilter === 'all' ? undefined : statusFilter,
                  direction: directionFilter === 'all' ? undefined : directionFilter,
                }).unwrap()
              }
            />
            <SavedViews pageKey="payments" />
          </>
        }
        columns={[
          { header: 'เลขที่', cell: (p) => <span className="font-medium">{p.paymentNo}</span> },
          { header: 'ทิศทาง', cell: (p) => (p.direction === 'Inbound' ? 'รับเบี้ย' : 'จ่ายสินไหม') },
          {
            header: 'อ้างอิง',
            cell: (p) => (
              <div>
                <div>{p.policyNo ?? p.claimNo ?? '-'}</div>
                {p.installmentSeq != null && (
                  <div className="text-xs text-muted-foreground">
                    งวด {p.installmentSeq}
                    {p.dueDate ? ` · ครบกำหนด ${fmtDate(p.dueDate)}` : ''}
                  </div>
                )}
              </div>
            ),
          },
          { header: 'สถานะ', cell: (p) => <StatusBadge status={p.status} /> },
          { header: 'จำนวน', className: 'text-right tabular-nums', cell: (p) => fmtBaht(p.amount) },
          { header: 'ชำระเมื่อ', cell: (p) => (p.paidAt ? fmtDateTime(p.paidAt) : '-') },
          {
            header: 'จัดการ',
            className: 'text-right',
            cell: (p) =>
              p.status === 'Pending' ? (
                <div className="flex justify-end gap-2">
                  {p.direction === 'Inbound' && (
                    <PromptPayButton paymentId={p.id} paymentNo={p.paymentNo} amount={p.amount} />
                  )}
                  <Can permission={P.PaymentSettle}>
                    <Button
                      size="sm"
                      variant="outline"
                      onClick={() => {
                        setSettleFor(p);
                        setReferenceNo('');
                      }}
                    >
                      <Wallet /> ชำระ
                    </Button>
                  </Can>
                </div>
              ) : p.status === 'Paid' && p.direction === 'Inbound' ? (
                <Button size="sm" variant="ghost" onClick={() => downloadReceipt(p.id, p.paymentNo)}>
                  <Receipt /> ใบเสร็จ
                </Button>
              ) : null,
          },
        ]}
      />

      <Dialog open={!!settleFor} onOpenChange={(o) => !o && setSettleFor(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>ชำระเงิน {settleFor?.paymentNo}</DialogTitle>
            <DialogDescription>
              {settleFor?.direction === 'Inbound'
                ? 'ชำระเบี้ย — กรมธรรม์จะเปิดใช้งานอัตโนมัติ'
                : 'จ่ายสินไหม — เคลมจะเปลี่ยนเป็นชำระแล้ว'}
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-2">
            <Label htmlFor="ref">เลขอ้างอิงการชำระ</Label>
            <Input id="ref" value={referenceNo} onChange={(e) => setReferenceNo(e.target.value)} placeholder="TXN-001" />
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setSettleFor(null)}>
              ยกเลิก
            </Button>
            <Button onClick={submit} disabled={settling || !referenceNo}>
              ยืนยันชำระ
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}

export default function PaymentsPage() {
  return (
    <Suspense fallback={null}>
      <PaymentsPageContent />
    </Suspense>
  );
}
