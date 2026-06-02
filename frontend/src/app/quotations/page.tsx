'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { toast } from 'sonner';
import { Plus, FileSignature } from 'lucide-react';
import {
  useGetQuotationsQuery,
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

const today = () => new Date().toISOString().slice(0, 10);
const PAGE_SIZE = 10;

export default function QuotationsPage() {
  const router = useRouter();
  const [page, setPage] = useState(1);
  const [searchInput, setSearchInput] = useState('');
  const search = useDebouncedValue(searchInput, 300);
  const { data, isFetching } = useGetQuotationsQuery({ page, pageSize: PAGE_SIZE, search });
  const [issuePolicy, { isLoading: issuing }] = useIssuePolicyMutation();

  const [issueFor, setIssueFor] = useState<QuotationDto | null>(null);
  const [effectiveDate, setEffectiveDate] = useState(today());

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
          <Button onClick={() => router.push('/quotations/new')}>
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
