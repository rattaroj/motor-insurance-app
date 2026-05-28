'use client';

import { useState } from 'react';
import { toast } from 'sonner';
import { Wallet } from 'lucide-react';
import { useGetPaymentsQuery, useSettlePaymentMutation, type PaymentDto } from '@/lib/api/insuranceApi';
import { StatusBadge } from '@/components/StatusBadge';
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
import { apiError, fmtBaht, fmtDateTime } from '@/lib/utils';
import { useDebouncedValue } from '@/lib/use-debounced';

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

export default function PaymentsPage() {
  const [page, setPage] = useState(1);
  const [searchInput, setSearchInput] = useState('');
  const search = useDebouncedValue(searchInput, 300);
  const [statusFilter, setStatusFilter] = useState('all');
  const [directionFilter, setDirectionFilter] = useState('all');
  const { data, isFetching } = useGetPaymentsQuery({
    page,
    pageSize: PAGE_SIZE,
    search,
    status: statusFilter === 'all' ? undefined : statusFilter,
    direction: directionFilter === 'all' ? undefined : directionFilter,
  });
  const [settle, { isLoading: settling }] = useSettlePaymentMutation();
  const [settleFor, setSettleFor] = useState<PaymentDto | null>(null);
  const [referenceNo, setReferenceNo] = useState('');

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
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">การชำระเงิน</h1>
        <p className="text-sm text-muted-foreground">เบี้ยรับเข้า และสินไหมจ่ายออก</p>
      </div>

      <DataTable<PaymentDto>
        rows={data?.items}
        loading={isFetching}
        getKey={(p) => p.id}
        page={page}
        pageSize={PAGE_SIZE}
        totalCount={data?.totalCount ?? 0}
        onPageChange={setPage}
        search={searchInput}
        onSearchChange={(v) => {
          setSearchInput(v);
          setPage(1);
        }}
        searchPlaceholder="ค้นหาเลขที่ / อ้างอิง"
        emptyText="ไม่มีรายการชำระเงิน"
        toolbar={
          <>
            <Select
              value={statusFilter}
              onValueChange={(v) => {
                setStatusFilter(v);
                setPage(1);
              }}
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
              onValueChange={(v) => {
                setDirectionFilter(v);
                setPage(1);
              }}
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
          </>
        }
        columns={[
          { header: 'เลขที่', cell: (p) => <span className="font-medium">{p.paymentNo}</span> },
          { header: 'ทิศทาง', cell: (p) => (p.direction === 'Inbound' ? 'รับเบี้ย' : 'จ่ายสินไหม') },
          { header: 'อ้างอิง', cell: (p) => p.policyNo ?? p.claimNo ?? '-' },
          { header: 'สถานะ', cell: (p) => <StatusBadge status={p.status} /> },
          { header: 'จำนวน', className: 'text-right tabular-nums', cell: (p) => fmtBaht(p.amount) },
          { header: 'ชำระเมื่อ', cell: (p) => (p.paidAt ? fmtDateTime(p.paidAt) : '-') },
          {
            header: 'จัดการ',
            className: 'text-right',
            cell: (p) =>
              p.status === 'Pending' ? (
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
