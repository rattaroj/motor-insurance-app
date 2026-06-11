'use client';

import { Suspense, useState } from 'react';
import Link from 'next/link';
import { toast } from 'sonner';
import { Plus, ChevronRight, Check, X, Wrench, Image as ImageIcon, FileWarning } from 'lucide-react';
import {
  useGetClaimsQuery,
  useFileClaimMutation,
  useAdvanceClaimMutation,
  useApproveClaimMutation,
  useRejectClaimMutation,
  useGetPoliciesQuery,
  useGetGaragesQuery,
  useBulkAssignClaimsMutation,
  useExportClaimsMutation,
  type ClaimStatus,
  type ClaimDto,
} from '@/lib/api/insuranceApi';
import { StatusBadge } from '@/components/StatusBadge';
import { ExportButton } from '@/components/export-button';
import { DataTable } from '@/components/data-table';
import { Button } from '@/components/ui/button';
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
import { Textarea } from '@/components/ui/textarea';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Can } from '@/components/can';
import { ClaimManageDialog } from '@/components/claim-manage-dialog';
import { ClaimsAgingPanel } from '@/components/claims-aging-panel';
import { PageHeader } from '@/components/page-header';
import { SavedViews } from '@/components/saved-views';
import { P } from '@/lib/auth/permissions';
import { apiError, fmtBaht, fmtDate } from '@/lib/utils';
import { useListUrlState } from '@/lib/use-url-state';

const today = () => new Date().toISOString().slice(0, 10);

const advanceMap: Partial<Record<ClaimStatus, { to: ClaimStatus; label: string }>> = {
  Filed: { to: 'UnderReview', label: 'เริ่มตรวจสอบ' },
  UnderReview: { to: 'Assessment', label: 'เริ่มประเมิน' },
  Paid: { to: 'Closed', label: 'ปิดเคลม' },
  Rejected: { to: 'Closed', label: 'ปิดเคลม' },
};

const CLAIM_STATUSES: { value: string; label: string }[] = [
  { value: 'all', label: 'ทุกสถานะ' },
  { value: 'Filed', label: 'แจ้งเคลม' },
  { value: 'UnderReview', label: 'กำลังตรวจสอบ' },
  { value: 'Assessment', label: 'ประเมิน' },
  { value: 'Approved', label: 'อนุมัติ' },
  { value: 'Rejected', label: 'ปฏิเสธ' },
  { value: 'Paid', label: 'ชำระแล้ว' },
  { value: 'Closed', label: 'ปิด' },
];

const PAGE_SIZE = 10;

function ClaimsPageContent() {
  const { page, setPage, searchInput, onSearchChange, search, filters, setFilter } = useListUrlState(['status']);
  const statusFilter = filters.status;
  const { data, isFetching } = useGetClaimsQuery({
    page,
    pageSize: PAGE_SIZE,
    search,
    status: statusFilter === 'all' ? undefined : statusFilter,
  });
  const { data: policies } = useGetPoliciesQuery({ page: 1, pageSize: 100 });
  const [fileClaim, { isLoading: filing }] = useFileClaimMutation();
  const [advance] = useAdvanceClaimMutation();
  const [approve, { isLoading: approving }] = useApproveClaimMutation();
  const [reject, { isLoading: rejecting }] = useRejectClaimMutation();
  const [exportClaims] = useExportClaimsMutation();

  const [fileOpen, setFileOpen] = useState(false);
  const [form, setForm] = useState({ policyId: '', incidentDate: today(), description: '', claimedAmount: '' });
  const [approveFor, setApproveFor] = useState<ClaimDto | null>(null);
  const [approvedAmount, setApprovedAmount] = useState('');
  const [rejectFor, setRejectFor] = useState<ClaimDto | null>(null);
  const [reason, setReason] = useState('');
  const [manageId, setManageId] = useState<number | null>(null);

  // Bulk assignment of garage/surveyor to selected claims.
  const { data: garages } = useGetGaragesQuery();
  const [bulkAssign, { isLoading: bulkAssigning }] = useBulkAssignClaimsMutation();
  const [selected, setSelected] = useState<Set<number>>(new Set());
  const [bulkOpen, setBulkOpen] = useState(false);
  const [bulkGarage, setBulkGarage] = useState('none');
  const [bulkSurveyor, setBulkSurveyor] = useState('');

  const rows = data?.items ?? [];
  const allSelected = rows.length > 0 && rows.every((c) => selected.has(c.id));
  const toggle = (id: number) =>
    setSelected((prev) => {
      const next = new Set(prev);
      next.has(id) ? next.delete(id) : next.add(id);
      return next;
    });
  const toggleAll = () =>
    setSelected(allSelected ? new Set() : new Set(rows.map((c) => c.id)));

  const submitBulkAssign = async () => {
    const ok = await run(
      () =>
        bulkAssign({
          claimIds: [...selected],
          garageId: bulkGarage === 'none' ? null : Number(bulkGarage),
          surveyorName: bulkSurveyor.trim() || null,
        }).unwrap(),
      'มอบหมายเคลมที่เลือกแล้ว',
    );
    if (ok) {
      setBulkOpen(false);
      setSelected(new Set());
      setBulkGarage('none');
      setBulkSurveyor('');
    }
  };

  const activePolicies = (policies?.items ?? []).filter((p) => p.status === 'Active');

  const run = async (fn: () => Promise<unknown>, ok: string) => {
    try {
      await fn();
      toast.success(ok);
      return true;
    } catch (e) {
      toast.error(apiError(e));
      return false;
    }
  };

  return (
    <div className="space-y-6">
      <PageHeader
        icon={FileWarning}
        title="เคลม"
        description="แจ้งเคลม → ตรวจสอบ → ประเมิน → อนุมัติ/ปฏิเสธ"
        actions={
          <Can permission={P.ClaimFile}>
            <Button onClick={() => setFileOpen(true)}>
              <Plus /> แจ้งเคลม
            </Button>
          </Can>
        }
      />

      <ClaimsAgingPanel />

      {/* Bulk action bar — only when at least one claim is selected. */}
      {selected.size > 0 && (
        <Can permission={P.ClaimReview}>
          <div className="flex items-center justify-between rounded-lg border bg-muted/40 px-4 py-2 text-sm">
            <span>เลือก {selected.size} เคลม</span>
            <div className="flex gap-2">
              <Button size="sm" variant="ghost" onClick={() => setSelected(new Set())}>
                ล้าง
              </Button>
              <Button size="sm" onClick={() => setBulkOpen(true)}>
                <Wrench /> มอบหมายที่เลือก
              </Button>
            </div>
          </div>
        </Can>
      )}

      <DataTable<ClaimDto>
        rows={data?.items}
        loading={isFetching}
        getKey={(c) => c.id}
        page={page}
        pageSize={PAGE_SIZE}
        totalCount={data?.totalCount ?? 0}
        onPageChange={setPage}
        search={searchInput}
        onSearchChange={onSearchChange}
        searchPlaceholder="ค้นหาเลขเคลม / กรมธรรม์"
        emptyText="ยังไม่มีเคลม"
        toolbar={
          <>
            <Select
              value={statusFilter}
              onValueChange={(v) => setFilter('status', v)}
            >
              <SelectTrigger className="w-40">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {CLAIM_STATUSES.map((s) => (
                  <SelectItem key={s.value} value={s.value}>
                    {s.label}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
            <ExportButton
              filename="claims.csv"
              fetchUrl={() =>
                exportClaims({
                  search: search || undefined,
                  status: statusFilter === 'all' ? undefined : statusFilter,
                }).unwrap()
              }
            />
            <SavedViews pageKey="claims" />
          </>
        }
        columns={[
          {
            header: (
              <input
                type="checkbox"
                aria-label="เลือกทั้งหมด"
                className="h-4 w-4 cursor-pointer accent-primary"
                checked={allSelected}
                onChange={toggleAll}
                disabled={rows.length === 0}
              />
            ),
            className: 'w-10',
            cell: (c) => (
              <input
                type="checkbox"
                aria-label={`เลือก ${c.claimNo}`}
                className="h-4 w-4 cursor-pointer accent-primary"
                checked={selected.has(c.id)}
                onChange={() => toggle(c.id)}
              />
            ),
          },
          {
            header: 'เลขที่',
            cell: (c) => (
              <Link href={`/claims/${c.id}`} className="font-medium text-primary hover:underline">
                {c.claimNo}
              </Link>
            ),
          },
          { header: 'กรมธรรม์', cell: (c) => c.policyNo },
          { header: 'วันเกิดเหตุ', cell: (c) => fmtDate(c.incidentDate) },
          { header: 'สถานะ', cell: (c) => <StatusBadge status={c.status} /> },
          { header: 'เรียกร้อง', className: 'text-right tabular-nums', cell: (c) => fmtBaht(c.claimedAmount) },
          {
            header: 'อนุมัติ',
            className: 'text-right tabular-nums',
            cell: (c) => (c.approvedAmount != null ? fmtBaht(c.approvedAmount) : '-'),
          },
          {
            header: 'จัดการ',
            className: 'text-right',
            cell: (c) => {
              const adv = advanceMap[c.status];
              return (
                <div className="flex justify-end gap-2">
                  <Button size="sm" variant="ghost" onClick={() => setManageId(c.id)}>
                    <Wrench /> จัดการ
                    {c.photoCount > 0 && (
                      <span className="ml-1 inline-flex items-center gap-0.5 text-xs text-muted-foreground">
                        <ImageIcon className="h-3 w-3" />
                        {c.photoCount}
                      </span>
                    )}
                  </Button>
                  {adv && (
                    <Can permission={P.ClaimReview}>
                      <Button
                        size="sm"
                        variant="outline"
                        onClick={() => run(() => advance({ id: c.id, to: adv.to }).unwrap(), 'อัปเดตสถานะแล้ว')}
                      >
                        {adv.label} <ChevronRight />
                      </Button>
                    </Can>
                  )}
                  {c.status === 'Assessment' && (
                    <>
                      <Can permission={P.ClaimApprove}>
                        <Button
                          size="sm"
                          onClick={() => {
                            setApproveFor(c);
                            setApprovedAmount(String(c.claimedAmount));
                          }}
                        >
                          <Check /> อนุมัติ
                        </Button>
                      </Can>
                      <Can permission={P.ClaimReject}>
                        <Button
                          size="sm"
                          variant="destructive"
                          onClick={() => {
                            setRejectFor(c);
                            setReason('');
                          }}
                        >
                          <X /> ปฏิเสธ
                        </Button>
                      </Can>
                    </>
                  )}
                  {c.status === 'Approved' && (
                    <span className="text-xs text-muted-foreground">รอจ่ายสินไหม</span>
                  )}
                </div>
              );
            },
          },
        ]}
      />

      {/* File claim */}
      <Dialog open={fileOpen} onOpenChange={setFileOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>แจ้งเคลม</DialogTitle>
            <DialogDescription>เลือกกรมธรรม์ที่คุ้มครองอยู่</DialogDescription>
          </DialogHeader>
          <div className="space-y-4">
            <div className="space-y-2">
              <Label required>กรมธรรม์</Label>
              <Select value={form.policyId} onValueChange={(v) => setForm({ ...form, policyId: v })}>
                <SelectTrigger>
                  <SelectValue placeholder="เลือกกรมธรรม์ (Active)" />
                </SelectTrigger>
                <SelectContent>
                  {activePolicies.map((p) => (
                    <SelectItem key={p.id} value={String(p.id)}>
                      {p.policyNo} · {p.customerName}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-2">
                <Label htmlFor="incident" required>วันเกิดเหตุ</Label>
                <Input
                  id="incident"
                  type="date"
                  value={form.incidentDate}
                  onChange={(e) => setForm({ ...form, incidentDate: e.target.value })}
                />
              </div>
              <div className="space-y-2">
                <Label htmlFor="claimed" required>เรียกร้อง (บาท)</Label>
                <Input
                  id="claimed"
                  type="number"
                  value={form.claimedAmount}
                  onChange={(e) => setForm({ ...form, claimedAmount: e.target.value })}
                />
              </div>
            </div>
            <div className="space-y-2">
              <Label htmlFor="desc">รายละเอียด</Label>
              <Textarea id="desc" value={form.description} onChange={(e) => setForm({ ...form, description: e.target.value })} />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setFileOpen(false)}>
              ยกเลิก
            </Button>
            <Button
              disabled={filing || !form.policyId || !form.claimedAmount}
              onClick={async () => {
                const ok = await run(
                  () =>
                    fileClaim({
                      policyId: Number(form.policyId),
                      incidentDate: form.incidentDate,
                      description: form.description || undefined,
                      claimedAmount: Number(form.claimedAmount),
                    }).unwrap(),
                  'แจ้งเคลมแล้ว',
                );
                if (ok) {
                  setFileOpen(false);
                  setForm({ policyId: '', incidentDate: today(), description: '', claimedAmount: '' });
                }
              }}
            >
              แจ้งเคลม
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Approve */}
      <Dialog open={!!approveFor} onOpenChange={(o) => !o && setApproveFor(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>อนุมัติเคลม {approveFor?.claimNo}</DialogTitle>
            <DialogDescription>ระบบจะสร้างรายการจ่ายสินไหม (รอชำระ) ให้อัตโนมัติ</DialogDescription>
          </DialogHeader>
          <div className="space-y-2">
            <Label htmlFor="approved" required>จำนวนที่อนุมัติ (ไม่เกิน {fmtBaht(approveFor?.claimedAmount ?? 0)})</Label>
            <Input id="approved" type="number" value={approvedAmount} onChange={(e) => setApprovedAmount(e.target.value)} />
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setApproveFor(null)}>
              ยกเลิก
            </Button>
            <Button
              disabled={approving || !approvedAmount}
              onClick={async () => {
                if (!approveFor) return;
                const ok = await run(
                  () => approve({ id: approveFor.id, approvedAmount: Number(approvedAmount) }).unwrap(),
                  'อนุมัติเคลมแล้ว',
                );
                if (ok) setApproveFor(null);
              }}
            >
              อนุมัติ
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Reject */}
      <Dialog open={!!rejectFor} onOpenChange={(o) => !o && setRejectFor(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>ปฏิเสธเคลม {rejectFor?.claimNo}</DialogTitle>
            <DialogDescription>ระบุเหตุผลการปฏิเสธ</DialogDescription>
          </DialogHeader>
          <div className="space-y-2">
            <Label htmlFor="rreason" required>เหตุผล</Label>
            <Textarea id="rreason" value={reason} onChange={(e) => setReason(e.target.value)} />
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setRejectFor(null)}>
              ยกเลิก
            </Button>
            <Button
              variant="destructive"
              disabled={rejecting || !reason}
              onClick={async () => {
                if (!rejectFor) return;
                const ok = await run(() => reject({ id: rejectFor.id, reason }).unwrap(), 'ปฏิเสธเคลมแล้ว');
                if (ok) setRejectFor(null);
              }}
            >
              ยืนยันปฏิเสธ
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <ClaimManageDialog claimId={manageId} onClose={() => setManageId(null)} />

      {/* Bulk assign garage/surveyor to the selected claims. */}
      <Dialog open={bulkOpen} onOpenChange={setBulkOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>มอบหมายเคลมที่เลือก</DialogTitle>
            <DialogDescription>กำหนดอู่/ศูนย์ซ่อมและเจ้าหน้าที่สำรวจให้ {selected.size} เคลมที่เลือกพร้อมกัน</DialogDescription>
          </DialogHeader>
          <div className="space-y-4">
            <div className="space-y-2">
              <Label>อู่/ศูนย์ซ่อม</Label>
              <Select value={bulkGarage} onValueChange={setBulkGarage}>
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="none">— ไม่ระบุ —</SelectItem>
                  {garages?.map((g) => (
                    <SelectItem key={g.id} value={String(g.id)}>
                      {g.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2">
              <Label htmlFor="bulk-surveyor">เจ้าหน้าที่สำรวจ</Label>
              <Input
                id="bulk-surveyor"
                value={bulkSurveyor}
                onChange={(e) => setBulkSurveyor(e.target.value)}
                placeholder="ชื่อเจ้าหน้าที่สำรวจ (ไม่บังคับ)"
              />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setBulkOpen(false)}>
              ยกเลิก
            </Button>
            <Button onClick={submitBulkAssign} disabled={bulkAssigning}>
              <Wrench /> มอบหมาย {selected.size} เคลม
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}

export default function ClaimsPage() {
  return (
    <Suspense fallback={null}>
      <ClaimsPageContent />
    </Suspense>
  );
}
