'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { toast } from 'sonner';
import { Plus, FileSignature } from 'lucide-react';
import {
  useGetQuotationsQuery,
  useCreateQuotationMutation,
  useGetCustomersQuery,
  useGetVehiclesQuery,
  useIssuePolicyMutation,
  type CoverageType,
  type QuotationDto,
} from '@/lib/api/insuranceApi';
import { Button } from '@/components/ui/button';
import { DataTable } from '@/components/data-table';
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
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Can } from '@/components/can';
import { P } from '@/lib/auth/permissions';
import { apiError, fmtBaht, fmtDate } from '@/lib/utils';
import { useDebouncedValue } from '@/lib/use-debounced';

const COVERAGES: { value: CoverageType; label: string }[] = [
  { value: 'Type1', label: 'ชั้น 1' },
  { value: 'Type2Plus', label: 'ชั้น 2+' },
  { value: 'Type3Plus', label: 'ชั้น 3+' },
  { value: 'Type3', label: 'ชั้น 3' },
];

const emptyCreate = { customerId: '', vehicleId: '', coverageType: '' as CoverageType | '', sumInsured: '' };
const today = () => new Date().toISOString().slice(0, 10);
const PAGE_SIZE = 10;

export default function QuotationsPage() {
  const router = useRouter();
  const [page, setPage] = useState(1);
  const [searchInput, setSearchInput] = useState('');
  const search = useDebouncedValue(searchInput, 300);
  const { data, isFetching } = useGetQuotationsQuery({ page, pageSize: PAGE_SIZE, search });
  const { data: customers } = useGetCustomersQuery({ pageSize: 100 });
  const [createQuotation, { isLoading: creating }] = useCreateQuotationMutation();
  const [issuePolicy, { isLoading: issuing }] = useIssuePolicyMutation();

  const [open, setOpen] = useState(false);
  const [form, setForm] = useState(emptyCreate);
  const [issueFor, setIssueFor] = useState<QuotationDto | null>(null);
  const [effectiveDate, setEffectiveDate] = useState(today());

  const { data: vehicles } = useGetVehiclesQuery(
    { customerId: Number(form.customerId), pageSize: 100 },
    { skip: !form.customerId },
  );
  const vehiclesOfCustomer = vehicles?.items ?? [];

  const submitCreate = async () => {
    try {
      await createQuotation({
        customerId: Number(form.customerId),
        vehicleId: Number(form.vehicleId),
        coverageType: form.coverageType as CoverageType,
        sumInsured: Number(form.sumInsured),
      }).unwrap();
      toast.success('สร้างใบเสนอราคาแล้ว');
      setOpen(false);
      setForm(emptyCreate);
    } catch (e) {
      toast.error(apiError(e));
    }
  };

  const submitIssue = async () => {
    if (!issueFor) return;
    try {
      const res = await issuePolicy({ quotationId: issueFor.id, effectiveDate }).unwrap();
      toast.success('ออกกรมธรรม์แล้ว', { description: `กรมธรรม์ #${res.id}` });
      setIssueFor(null);
      router.push(`/policies/${res.id}`);
    } catch (e) {
      toast.error(apiError(e));
    }
  };

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">ใบเสนอราคา</h1>
          <p className="text-sm text-muted-foreground">คำนวณเบี้ยและออกกรมธรรม์</p>
        </div>
        <Can permission={P.QuotationWrite}>
          <Button onClick={() => setOpen(true)}>
            <Plus /> สร้างใบเสนอราคา
          </Button>
        </Can>
      </div>

      <DataTable<QuotationDto>
        rows={data?.items}
        loading={isFetching}
        getKey={(q) => q.id}
        page={page}
        pageSize={PAGE_SIZE}
        totalCount={data?.totalCount ?? 0}
        onPageChange={setPage}
        search={searchInput}
        onSearchChange={(v) => {
          setSearchInput(v);
          setPage(1);
        }}
        searchPlaceholder="ค้นหาเลขที่ / ลูกค้า / ทะเบียน"
        emptyText="ยังไม่มีใบเสนอราคา"
        columns={[
          { header: 'เลขที่', cell: (q) => <span className="font-medium">{q.quotationNo}</span> },
          { header: 'ลูกค้า', cell: (q) => q.customerName },
          { header: 'ทะเบียน', cell: (q) => q.vehicleRegistration },
          { header: 'ความคุ้มครอง', cell: (q) => COVERAGES.find((c) => c.value === q.coverageType)?.label ?? q.coverageType },
          { header: 'ทุนประกัน', className: 'text-right tabular-nums', cell: (q) => fmtBaht(q.sumInsured) },
          { header: 'เบี้ย', className: 'text-right tabular-nums', cell: (q) => fmtBaht(q.premium) },
          { header: 'ใช้ได้ถึง', cell: (q) => fmtDate(q.validUntil) },
          {
            header: 'จัดการ',
            className: 'text-right',
            cell: (q) => (
              <Can permission={P.PolicyIssue}>
                <Button
                  size="sm"
                  variant="outline"
                  onClick={() => {
                    setIssueFor(q);
                    setEffectiveDate(today());
                  }}
                >
                  <FileSignature /> ออกกรมธรรม์
                </Button>
              </Can>
            ),
          },
        ]}
      />

      {/* Create quotation */}
      <Dialog open={open} onOpenChange={setOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>สร้างใบเสนอราคา</DialogTitle>
            <DialogDescription>ระบบจะคำนวณเบี้ยให้อัตโนมัติตามชั้นความคุ้มครอง</DialogDescription>
          </DialogHeader>
          <div className="space-y-4">
            <div className="space-y-2">
              <Label required>ลูกค้า</Label>
              <Select
                value={form.customerId}
                onValueChange={(v) => setForm({ ...form, customerId: v, vehicleId: '' })}
              >
                <SelectTrigger>
                  <SelectValue placeholder="เลือกลูกค้า" />
                </SelectTrigger>
                <SelectContent>
                  {customers?.items.map((c) => (
                    <SelectItem key={c.id} value={String(c.id)}>
                      {c.fullName}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2">
              <Label required>รถยนต์</Label>
              <Select
                value={form.vehicleId}
                onValueChange={(v) => setForm({ ...form, vehicleId: v })}
                disabled={!form.customerId}
              >
                <SelectTrigger>
                  <SelectValue placeholder={form.customerId ? 'เลือกรถ' : 'เลือกลูกค้าก่อน'} />
                </SelectTrigger>
                <SelectContent>
                  {vehiclesOfCustomer.map((v) => (
                    <SelectItem key={v.id} value={String(v.id)}>
                      {v.registrationNo} · {v.brand} {v.model}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-2">
                <Label required>ความคุ้มครอง</Label>
                <Select
                  value={form.coverageType}
                  onValueChange={(v) => setForm({ ...form, coverageType: v as CoverageType })}
                >
                  <SelectTrigger>
                    <SelectValue placeholder="เลือกชั้น" />
                  </SelectTrigger>
                  <SelectContent>
                    {COVERAGES.map((c) => (
                      <SelectItem key={c.value} value={c.value}>
                        {c.label}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div className="space-y-2">
                <Label htmlFor="sum" required>ทุนประกัน (บาท)</Label>
                <Input
                  id="sum"
                  type="number"
                  value={form.sumInsured}
                  onChange={(e) => setForm({ ...form, sumInsured: e.target.value })}
                  placeholder="500000"
                />
              </div>
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setOpen(false)}>
              ยกเลิก
            </Button>
            <Button
              onClick={submitCreate}
              disabled={creating || !form.customerId || !form.vehicleId || !form.coverageType || !form.sumInsured}
            >
              {creating ? 'กำลังคำนวณ…' : 'สร้าง'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Issue policy */}
      <Dialog open={!!issueFor} onOpenChange={(o) => !o && setIssueFor(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>ออกกรมธรรม์</DialogTitle>
            <DialogDescription>
              จากใบเสนอราคา {issueFor?.quotationNo} — {issueFor?.customerName}
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-2">
            <Label htmlFor="eff" required>วันที่เริ่มคุ้มครอง</Label>
            <Input id="eff" type="date" value={effectiveDate} onChange={(e) => setEffectiveDate(e.target.value)} />
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setIssueFor(null)}>
              ยกเลิก
            </Button>
            <Button onClick={submitIssue} disabled={issuing || !effectiveDate}>
              {issuing ? 'กำลังออก…' : 'ออกกรมธรรม์'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
