'use client';

import { use, useState } from 'react';
import Link from 'next/link';
import { toast } from 'sonner';
import { ArrowLeft, CheckCircle, XCircle, RefreshCw, Plus, Wallet, FileSignature, FileDown, Receipt, SlidersHorizontal, Check } from 'lucide-react';
import {
  useGetPolicyQuery,
  useGetPolicyHistoryQuery,
  useGetPolicyActivityQuery,
  useGetPaymentsQuery,
  useGetClaimsQuery,
  useGetRidersQuery,
  useActivatePolicyMutation,
  useCancelPolicyMutation,
  useRenewPolicyMutation,
  useSettlePaymentMutation,
  useFileClaimMutation,
  useCreateEndorsementMutation,
  useCoverageEndorsementMutation,
  useGetPolicyDocumentMutation,
  useGetPaymentReceiptMutation,
  fileUrl,
  type CoverageType,
} from '@/lib/api/insuranceApi';
import { StatusBadge } from '@/components/StatusBadge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from '@/components/ui/table';
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
import { Separator } from '@/components/ui/separator';
import { Skeleton } from '@/components/ui/skeleton';
import { Can } from '@/components/can';
import { PromptPayButton } from '@/components/promptpay-button';
import { ImageGallery } from '@/components/image-preview';
import { P } from '@/lib/auth/permissions';
import { apiError, cn, fmtBaht, fmtDate, fmtDateTime, saveUrl } from '@/lib/utils';

const today = () => new Date().toISOString().slice(0, 10);

const COVERAGES: { value: CoverageType; label: string }[] = [
  { value: 'Type1', label: 'ชั้น 1' },
  { value: 'Type2Plus', label: 'ชั้น 2+' },
  { value: 'Type3Plus', label: 'ชั้น 3+' },
  { value: 'Type3', label: 'ชั้น 3' },
];

export default function PolicyDetailPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params);
  const policyId = Number(id);

  const { data: policy, isLoading } = useGetPolicyQuery(policyId);
  const { data: history } = useGetPolicyHistoryQuery(policyId);
  const { data: activity } = useGetPolicyActivityQuery(policyId);
  const { data: payments } = useGetPaymentsQuery({ policyId, pageSize: 100 });
  const { data: claims } = useGetClaimsQuery({ policyId, pageSize: 100 });

  const [activate, { isLoading: activating }] = useActivatePolicyMutation();
  const [cancel, { isLoading: cancelling }] = useCancelPolicyMutation();
  const [renew, { isLoading: renewing }] = useRenewPolicyMutation();
  const [settle, { isLoading: settling }] = useSettlePaymentMutation();
  const [fileClaim, { isLoading: filing }] = useFileClaimMutation();
  const [endorse, { isLoading: endorsing }] = useCreateEndorsementMutation();
  const [coverageEndorse, { isLoading: coverageEndorsing }] = useCoverageEndorsementMutation();
  const [getPolicyPdf, { isLoading: pdfLoading }] = useGetPolicyDocumentMutation();
  const [getReceipt] = useGetPaymentReceiptMutation();
  const { data: riders } = useGetRidersQuery();

  const [cancelOpen, setCancelOpen] = useState(false);
  const [reason, setReason] = useState('');
  const [settleFor, setSettleFor] = useState<number | null>(null);
  const [referenceNo, setReferenceNo] = useState('');
  const [claimOpen, setClaimOpen] = useState(false);
  const [claimForm, setClaimForm] = useState({ incidentDate: today(), description: '', claimedAmount: '' });
  const [endorseOpen, setEndorseOpen] = useState(false);
  const [endorseForm, setEndorseForm] = useState({ fullName: '', phone: '', email: '', effectiveDate: today(), note: '' });
  const [coverageOpen, setCoverageOpen] = useState(false);
  const [coverageForm, setCoverageForm] = useState<{
    coverageType: CoverageType;
    sumInsured: string;
    riderIds: number[];
    effectiveDate: string;
    note: string;
  }>({ coverageType: 'Type1', sumInsured: '', riderIds: [], effectiveDate: today(), note: '' });

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

  const downloadPolicyPdf = async () => {
    try {
      const url = await getPolicyPdf(policyId).unwrap();
      saveUrl(url, `${policy?.policyNo ?? 'policy'}.pdf`);
    } catch (e) {
      toast.error(apiError(e));
    }
  };

  const downloadReceipt = async (paymentId: number, paymentNo: string) => {
    try {
      const url = await getReceipt(paymentId).unwrap();
      saveUrl(url, `${paymentNo}.pdf`);
    } catch (e) {
      toast.error(apiError(e));
    }
  };

  if (isLoading) return <PolicyDetailSkeleton />;
  if (!policy) return <p className="text-sm text-destructive">ไม่พบกรมธรรม์</p>;

  return (
    <div className="space-y-6">
      <Link href="/policies" className="inline-flex items-center gap-1 text-sm text-muted-foreground hover:text-foreground">
        <ArrowLeft className="h-4 w-4" /> กลับ
      </Link>

      <div className="flex flex-wrap items-start justify-between gap-4">
        <div>
          <div className="flex items-center gap-3">
            <h1 className="text-2xl font-semibold tracking-tight">{policy.policyNo}</h1>
            <StatusBadge status={policy.status} />
          </div>
          <p className="text-sm text-muted-foreground">
            {policy.customerName} · {policy.vehicleRegistration}
          </p>
        </div>
        <div className="flex flex-wrap gap-2">
          <Button variant="outline" disabled={pdfLoading} onClick={downloadPolicyPdf}>
            <FileDown /> {pdfLoading ? 'กำลังสร้าง…' : 'PDF กรมธรรม์'}
          </Button>
          <Can permission={P.PolicyActivate}>
            <Button
              disabled={activating || policy.status !== 'Issued'}
              onClick={() => run(() => activate(policyId).unwrap(), 'เปิดใช้งานกรมธรรม์แล้ว')}
            >
              <CheckCircle /> เปิดใช้งาน
            </Button>
          </Can>
          <Can permission={P.PolicyRenew}>
            <Button
              variant="outline"
              disabled={renewing || policy.status !== 'Active'}
              onClick={() => run(() => renew({ policyId }).unwrap(), 'สร้างกรมธรรม์ต่ออายุแล้ว')}
            >
              <RefreshCw /> ต่ออายุ
            </Button>
          </Can>
          <Can permission={P.PolicyCancel}>
            <Button
              variant="destructive"
              disabled={cancelling || !['Issued', 'Active'].includes(policy.status)}
              onClick={() => {
                setReason('');
                setCancelOpen(true);
              }}
            >
              <XCircle /> ยกเลิก
            </Button>
          </Can>
        </div>
      </div>

      <div className="grid gap-4 sm:grid-cols-4">
        {[
          { label: 'ความคุ้มครอง', value: policy.coverageType },
          { label: 'ทุนประกัน', value: fmtBaht(policy.sumInsured) },
          { label: 'เบี้ย', value: fmtBaht(policy.premium) },
          { label: 'คุ้มครอง', value: `${fmtDate(policy.effectiveDate)} – ${fmtDate(policy.expiryDate)}` },
        ].map((f) => (
          <Card key={f.label}>
            <CardContent className="p-4">
              <p className="text-xs text-muted-foreground">{f.label}</p>
              <p className="mt-1 font-semibold tabular-nums">{f.value}</p>
            </CardContent>
          </Card>
        ))}
      </div>

      {/* Premium breakdown */}
      <Card>
        <CardHeader>
          <CardTitle className="text-base">รายละเอียดเบี้ยประกัน</CardTitle>
        </CardHeader>
        <CardContent className="pt-0">
          <dl className="space-y-1 text-sm">
            <div className="flex justify-between">
              <dt className="text-muted-foreground">เบี้ยฐาน</dt>
              <dd className="tabular-nums">{fmtBaht(policy.basePremium)}</dd>
            </div>
            <div className="flex justify-between">
              <dt className="text-muted-foreground">ส่วนลดประวัติดี (NCB)</dt>
              <dd className="tabular-nums">{policy.ncbPercent}%</dd>
            </div>
            <div className="flex justify-between">
              <dt className="text-muted-foreground">ค่าเสียหายส่วนแรก</dt>
              <dd className="tabular-nums">{policy.deductible > 0 ? fmtBaht(policy.deductible) : 'ไม่มี'}</dd>
            </div>
            <div className="flex justify-between">
              <dt className="text-muted-foreground">ความคุ้มครองเสริม</dt>
              <dd className="text-right">{policy.riders.length ? policy.riders.join(', ') : 'ไม่มี'}</dd>
            </div>
            <div className="mt-2 flex justify-between border-t pt-2 text-base font-semibold">
              <span>เบี้ยสุทธิ</span>
              <span className="tabular-nums text-primary">{fmtBaht(policy.premium)}</span>
            </div>
          </dl>
        </CardContent>
      </Card>

      {/* Payments */}
      <Card>
        <CardHeader className="flex-row items-center justify-between">
          <CardTitle className="text-base">การชำระเงิน</CardTitle>
        </CardHeader>
        <CardContent className="pt-0">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>เลขที่</TableHead>
                <TableHead>ทิศทาง</TableHead>
                <TableHead>สถานะ</TableHead>
                <TableHead className="text-right">จำนวน</TableHead>
                <TableHead className="text-right">จัดการ</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {(payments?.items ?? []).map((p) => (
                <TableRow key={p.id}>
                  <TableCell className="font-medium">{p.paymentNo}</TableCell>
                  <TableCell>{p.direction === 'Inbound' ? 'รับเบี้ย' : 'จ่ายสินไหม'}</TableCell>
                  <TableCell>
                    <StatusBadge status={p.status} />
                  </TableCell>
                  <TableCell className="text-right tabular-nums">{fmtBaht(p.amount)}</TableCell>
                  <TableCell className="text-right">
                    <div className="flex justify-end gap-2">
                      {p.status === 'Pending' && p.direction === 'Inbound' && (
                        <PromptPayButton paymentId={p.id} paymentNo={p.paymentNo} amount={p.amount} />
                      )}
                      {p.status === 'Pending' && (
                        <Can permission={P.PaymentSettle}>
                          <Button
                            size="sm"
                            variant="outline"
                            onClick={() => {
                              setSettleFor(p.id);
                              setReferenceNo('');
                            }}
                          >
                            <Wallet /> ชำระ
                          </Button>
                        </Can>
                      )}
                      {p.status === 'Paid' && p.direction === 'Inbound' && (
                        <Button size="sm" variant="ghost" onClick={() => downloadReceipt(p.id, p.paymentNo)}>
                          <Receipt /> ใบเสร็จ
                        </Button>
                      )}
                    </div>
                  </TableCell>
                </TableRow>
              ))}
              {(payments?.items ?? []).length === 0 && (
                <TableRow>
                  <TableCell colSpan={5} className="text-center text-muted-foreground">
                    ไม่มีรายการชำระเงิน
                  </TableCell>
                </TableRow>
              )}
            </TableBody>
          </Table>
        </CardContent>
      </Card>

      {/* Claims */}
      <Card>
        <CardHeader className="flex-row items-center justify-between">
          <CardTitle className="text-base">เคลม</CardTitle>
          <Can permission={P.ClaimFile}>
            <Button size="sm" variant="outline" onClick={() => setClaimOpen(true)} disabled={policy.status !== 'Active'}>
              <Plus /> แจ้งเคลม
            </Button>
          </Can>
        </CardHeader>
        <CardContent className="pt-0">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>เลขที่</TableHead>
                <TableHead>วันเกิดเหตุ</TableHead>
                <TableHead>สถานะ</TableHead>
                <TableHead className="text-right">เรียกร้อง</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {(claims?.items ?? []).map((c) => (
                <TableRow key={c.id}>
                  <TableCell>
                    <Link href="/claims" className="font-medium text-primary hover:underline">
                      {c.claimNo}
                    </Link>
                  </TableCell>
                  <TableCell>{fmtDate(c.incidentDate)}</TableCell>
                  <TableCell>
                    <StatusBadge status={c.status} />
                  </TableCell>
                  <TableCell className="text-right tabular-nums">{fmtBaht(c.claimedAmount)}</TableCell>
                </TableRow>
              ))}
              {(claims?.items ?? []).length === 0 && (
                <TableRow>
                  <TableCell colSpan={4} className="text-center text-muted-foreground">
                    ยังไม่มีเคลม
                  </TableCell>
                </TableRow>
              )}
            </TableBody>
          </Table>
        </CardContent>
      </Card>

      {/* Named drivers */}
      <Card>
        <CardHeader>
          <CardTitle className="text-base">ผู้ขับขี่ระบุชื่อ</CardTitle>
        </CardHeader>
        <CardContent className="pt-0">
          {policy.drivers.length === 0 ? (
            <p className="text-sm text-muted-foreground">ไม่มีผู้ขับขี่ระบุชื่อ</p>
          ) : (
            <ImageGallery
              items={policy.drivers.map((d) => ({
                src: fileUrl(d.idCardImagePath),
                alt: `บัตรประชาชน ${d.fullName}`,
                title: d.fullName,
                subtitle: d.nationalId,
              }))}
            />
          )}
        </CardContent>
      </Card>

      {/* Endorsements (สลักหลัง) */}
      <Card>
        <CardHeader className="flex-row items-center justify-between">
          <CardTitle className="text-base">การสลักหลัง</CardTitle>
          <Can permission={P.PolicyEndorse}>
            <div className="flex gap-2">
              <Button
                size="sm"
                variant="outline"
                disabled={!['Issued', 'Active'].includes(policy.status)}
                onClick={() => {
                  setEndorseForm({ fullName: policy.customerName, phone: '', email: '', effectiveDate: today(), note: '' });
                  setEndorseOpen(true);
                }}
              >
                <FileSignature /> แก้ข้อมูลลูกค้า
              </Button>
              <Button
                size="sm"
                variant="outline"
                disabled={policy.status !== 'Active'}
                onClick={() => {
                  setCoverageForm({
                    coverageType: policy.coverageType as CoverageType,
                    sumInsured: String(policy.sumInsured),
                    riderIds: [],
                    effectiveDate: today(),
                    note: '',
                  });
                  setCoverageOpen(true);
                }}
              >
                <SlidersHorizontal /> ปรับความคุ้มครอง
              </Button>
            </div>
          </Can>
        </CardHeader>
        <CardContent className="pt-0">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>เลขที่สลักหลัง</TableHead>
                <TableHead>รายการ</TableHead>
                <TableHead>เดิม</TableHead>
                <TableHead>ใหม่</TableHead>
                <TableHead>มีผล</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {policy.endorsements.map((e, i) => (
                <TableRow key={i}>
                  <TableCell className="font-medium">{e.endorsementNo}</TableCell>
                  <TableCell>{e.fieldName}</TableCell>
                  <TableCell className="text-muted-foreground">{e.oldValue ?? '-'}</TableCell>
                  <TableCell>{e.newValue ?? '-'}</TableCell>
                  <TableCell>{fmtDate(e.effectiveDate)}</TableCell>
                </TableRow>
              ))}
              {policy.endorsements.length === 0 && (
                <TableRow>
                  <TableCell colSpan={5} className="text-center text-muted-foreground">
                    ยังไม่มีการสลักหลัง
                  </TableCell>
                </TableRow>
              )}
            </TableBody>
          </Table>
        </CardContent>
      </Card>

      {/* Activity timeline (audit) */}
      <Card>
        <CardHeader>
          <CardTitle className="text-base">ไทม์ไลน์กิจกรรม</CardTitle>
        </CardHeader>
        <CardContent className="pt-0">
          {(activity ?? []).length === 0 ? (
            <p className="text-sm text-muted-foreground">ยังไม่มีกิจกรรม</p>
          ) : (
            <ol className="space-y-3">
              {(activity ?? []).map((a, i) => (
                <li key={i} className="flex gap-3 text-sm">
                  <span
                    className={cn(
                      'mt-1.5 h-2 w-2 shrink-0 rounded-full',
                      a.type === 'status'
                        ? 'bg-blue-500'
                        : a.type === 'endorsement'
                          ? 'bg-purple-500'
                          : a.type === 'payment'
                            ? 'bg-emerald-500'
                            : 'bg-amber-500',
                    )}
                  />
                  <div className="min-w-0 flex-1">
                    <div className="flex items-baseline justify-between gap-2">
                      <span className="font-medium">{a.title}</span>
                      <span className="shrink-0 text-xs text-muted-foreground">{fmtDateTime(a.at)}</span>
                    </div>
                    {a.detail && <p className="text-muted-foreground">{a.detail}</p>}
                    {a.user && <p className="text-xs text-muted-foreground">โดย {a.user}</p>}
                  </div>
                </li>
              ))}
            </ol>
          )}
        </CardContent>
      </Card>

      {/* History */}
      <Card>
        <CardHeader>
          <CardTitle className="text-base">ประวัติการเปลี่ยนแปลง (temporal)</CardTitle>
        </CardHeader>
        <CardContent className="space-y-3 pt-0">
          {(history ?? []).map((h, i) => (
            <div key={i}>
              {i > 0 && <Separator className="my-3" />}
              <div className="flex items-center gap-3 text-sm">
                <StatusBadge status={h.status} />
                <span className="tabular-nums text-muted-foreground">{fmtBaht(h.premium)}</span>
                <span className="ml-auto text-xs text-muted-foreground">
                  {fmtDateTime(h.validFrom)} → {h.validTo.startsWith('9999') ? 'ปัจจุบัน' : fmtDateTime(h.validTo)}
                </span>
              </div>
            </div>
          ))}
          {(history ?? []).length === 0 && <p className="text-sm text-muted-foreground">ยังไม่มีประวัติ</p>}
        </CardContent>
      </Card>

      {/* Cancel dialog */}
      <Dialog open={cancelOpen} onOpenChange={setCancelOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>ยกเลิกกรมธรรม์</DialogTitle>
            <DialogDescription>ระบุเหตุผลการยกเลิก</DialogDescription>
          </DialogHeader>
          <div className="space-y-2">
            <Label htmlFor="reason" required>เหตุผล</Label>
            <Textarea id="reason" value={reason} onChange={(e) => setReason(e.target.value)} />
            {policy.status === 'Active' && (
              <p className="text-xs text-muted-foreground">
                กรมธรรม์ที่คุ้มครองอยู่จะคำนวณคืนเบี้ยตามสัดส่วนวันที่เหลือ (pro-rata) เป็นรายการจ่ายออกรอชำระ
              </p>
            )}
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setCancelOpen(false)}>
              ปิด
            </Button>
            <Button
              variant="destructive"
              disabled={cancelling || !reason}
              onClick={async () => {
                try {
                  const res = await cancel({ id: policyId, reason }).unwrap();
                  toast.success('ยกเลิกกรมธรรม์แล้ว', {
                    description:
                      res.refundAmount > 0
                        ? `คืนเบี้ย (pro-rata) ${fmtBaht(res.refundAmount)} — ${res.refundPaymentNo}`
                        : undefined,
                  });
                  setCancelOpen(false);
                } catch (e) {
                  toast.error(apiError(e));
                }
              }}
            >
              ยืนยันยกเลิก
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Settle dialog */}
      <Dialog open={settleFor !== null} onOpenChange={(o) => !o && setSettleFor(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>ชำระเงิน</DialogTitle>
            <DialogDescription>บันทึกการชำระเบี้ย — กรมธรรม์จะเปิดใช้งานอัตโนมัติเมื่อจ่ายเบี้ยแล้ว</DialogDescription>
          </DialogHeader>
          <div className="space-y-2">
            <Label htmlFor="ref" required>เลขอ้างอิงการชำระ</Label>
            <Input id="ref" value={referenceNo} onChange={(e) => setReferenceNo(e.target.value)} placeholder="TXN-001" />
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setSettleFor(null)}>
              ยกเลิก
            </Button>
            <Button
              disabled={settling || !referenceNo}
              onClick={async () => {
                if (settleFor === null) return;
                const ok = await run(() => settle({ id: settleFor, referenceNo }).unwrap(), 'บันทึกการชำระแล้ว');
                if (ok) setSettleFor(null);
              }}
            >
              ยืนยันชำระ
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* File claim dialog */}
      <Dialog open={claimOpen} onOpenChange={setClaimOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>แจ้งเคลม</DialogTitle>
            <DialogDescription>เปิดเคลมสำหรับกรมธรรม์ {policy.policyNo}</DialogDescription>
          </DialogHeader>
          <div className="space-y-4">
            <div className="space-y-2">
              <Label htmlFor="incident" required>วันเกิดเหตุ</Label>
              <Input
                id="incident"
                type="date"
                value={claimForm.incidentDate}
                onChange={(e) => setClaimForm({ ...claimForm, incidentDate: e.target.value })}
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="claimed" required>จำนวนเงินที่เรียกร้อง</Label>
              <Input
                id="claimed"
                type="number"
                value={claimForm.claimedAmount}
                onChange={(e) => setClaimForm({ ...claimForm, claimedAmount: e.target.value })}
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="desc">รายละเอียด</Label>
              <Textarea
                id="desc"
                value={claimForm.description}
                onChange={(e) => setClaimForm({ ...claimForm, description: e.target.value })}
              />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setClaimOpen(false)}>
              ยกเลิก
            </Button>
            <Button
              disabled={filing || !claimForm.incidentDate || !claimForm.claimedAmount}
              onClick={async () => {
                const ok = await run(
                  () =>
                    fileClaim({
                      policyId,
                      incidentDate: claimForm.incidentDate,
                      description: claimForm.description || undefined,
                      claimedAmount: Number(claimForm.claimedAmount),
                    }).unwrap(),
                  'แจ้งเคลมแล้ว',
                );
                if (ok) {
                  setClaimOpen(false);
                  setClaimForm({ incidentDate: today(), description: '', claimedAmount: '' });
                }
              }}
            >
              แจ้งเคลม
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Endorsement dialog (สลักหลัง) */}
      <Dialog open={endorseOpen} onOpenChange={setEndorseOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>ทำสลักหลังกรมธรรม์</DialogTitle>
            <DialogDescription>
              แก้ไขข้อมูลผู้เอาประกัน {policy.customerName} — กรอกเฉพาะช่องที่ต้องการเปลี่ยน
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-4">
            <div className="space-y-2">
              <Label htmlFor="endoName">ชื่อ-นามสกุล</Label>
              <Input
                id="endoName"
                value={endorseForm.fullName}
                onChange={(e) => setEndorseForm({ ...endorseForm, fullName: e.target.value })}
              />
            </div>
            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-2">
                <Label htmlFor="endoPhone">โทรศัพท์</Label>
                <Input
                  id="endoPhone"
                  value={endorseForm.phone}
                  onChange={(e) => setEndorseForm({ ...endorseForm, phone: e.target.value })}
                  placeholder="ปล่อยว่างหากไม่เปลี่ยน"
                />
              </div>
              <div className="space-y-2">
                <Label htmlFor="endoEmail">อีเมล</Label>
                <Input
                  id="endoEmail"
                  type="email"
                  value={endorseForm.email}
                  onChange={(e) => setEndorseForm({ ...endorseForm, email: e.target.value })}
                  placeholder="ปล่อยว่างหากไม่เปลี่ยน"
                />
              </div>
            </div>
            <div className="space-y-2">
              <Label htmlFor="endoEff" required>วันที่มีผล</Label>
              <Input
                id="endoEff"
                type="date"
                value={endorseForm.effectiveDate}
                onChange={(e) => setEndorseForm({ ...endorseForm, effectiveDate: e.target.value })}
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="endoNote">หมายเหตุ</Label>
              <Textarea
                id="endoNote"
                value={endorseForm.note}
                onChange={(e) => setEndorseForm({ ...endorseForm, note: e.target.value })}
              />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setEndorseOpen(false)}>
              ยกเลิก
            </Button>
            <Button
              disabled={endorsing || !endorseForm.effectiveDate}
              onClick={async () => {
                const ok = await run(
                  () =>
                    endorse({
                      policyId,
                      fullName: endorseForm.fullName || undefined,
                      phone: endorseForm.phone || undefined,
                      email: endorseForm.email || undefined,
                      effectiveDate: endorseForm.effectiveDate,
                      note: endorseForm.note || undefined,
                    }).unwrap(),
                  'บันทึกการสลักหลังแล้ว',
                );
                if (ok) setEndorseOpen(false);
              }}
            >
              บันทึกสลักหลัง
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Coverage endorsement dialog (ปรับความคุ้มครอง + เบี้ย pro-rata) */}
      <Dialog open={coverageOpen} onOpenChange={setCoverageOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>ปรับความคุ้มครอง</DialogTitle>
            <DialogDescription>
              เปลี่ยนชั้น/ทุนประกัน/ความคุ้มครองเสริม ระบบจะคิดเบี้ยเพิ่ม/คืนตามสัดส่วนวันที่เหลือ (pro-rata)
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-4">
            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-2">
                <Label required>ความคุ้มครอง</Label>
                <Select
                  value={coverageForm.coverageType}
                  onValueChange={(v) => setCoverageForm({ ...coverageForm, coverageType: v as CoverageType })}
                >
                  <SelectTrigger>
                    <SelectValue />
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
                <Label required>ทุนประกัน (บาท)</Label>
                <Input
                  type="number"
                  value={coverageForm.sumInsured}
                  onChange={(e) => setCoverageForm({ ...coverageForm, sumInsured: e.target.value })}
                />
              </div>
            </div>
            {!!riders?.length && (
              <div className="space-y-2">
                <Label>ความคุ้มครองเสริม (เลือกใหม่ทั้งหมด)</Label>
                <div className="grid gap-2 sm:grid-cols-2">
                  {riders.map((rd) => {
                    const on = coverageForm.riderIds.includes(rd.id);
                    return (
                      <button
                        key={rd.id}
                        type="button"
                        onClick={() =>
                          setCoverageForm((f) => ({
                            ...f,
                            riderIds: on ? f.riderIds.filter((x) => x !== rd.id) : [...f.riderIds, rd.id],
                          }))
                        }
                        className={cn(
                          'flex items-center justify-between rounded-md border px-3 py-2 text-left text-sm transition-colors',
                          on ? 'border-primary bg-primary/5' : 'hover:bg-muted',
                        )}
                      >
                        <span className="flex items-center gap-2">
                          <span
                            className={cn(
                              'flex h-4 w-4 items-center justify-center rounded border',
                              on ? 'border-primary bg-primary text-primary-foreground' : 'border-muted-foreground/40',
                            )}
                          >
                            {on && <Check className="h-3 w-3" />}
                          </span>
                          {rd.name}
                        </span>
                        <span className="tabular-nums text-muted-foreground">+{fmtBaht(rd.premium)}</span>
                      </button>
                    );
                  })}
                </div>
              </div>
            )}
            <div className="space-y-2">
              <Label required>วันที่มีผล</Label>
              <Input
                type="date"
                value={coverageForm.effectiveDate}
                onChange={(e) => setCoverageForm({ ...coverageForm, effectiveDate: e.target.value })}
              />
            </div>
            <div className="space-y-2">
              <Label>หมายเหตุ</Label>
              <Textarea
                value={coverageForm.note}
                onChange={(e) => setCoverageForm({ ...coverageForm, note: e.target.value })}
              />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setCoverageOpen(false)}>
              ยกเลิก
            </Button>
            <Button
              disabled={coverageEndorsing || !coverageForm.sumInsured || !coverageForm.effectiveDate}
              onClick={async () => {
                try {
                  const res = await coverageEndorse({
                    policyId,
                    newCoverageType: coverageForm.coverageType,
                    newSumInsured: Number(coverageForm.sumInsured),
                    newRiderIds: coverageForm.riderIds,
                    effectiveDate: coverageForm.effectiveDate,
                    note: coverageForm.note || undefined,
                  }).unwrap();
                  toast.success('ปรับความคุ้มครองแล้ว', {
                    description:
                      res.premiumDelta === 0
                        ? `เบี้ยใหม่ ${fmtBaht(res.newPremium)}`
                        : res.premiumDelta > 0
                          ? `เบี้ยเพิ่ม (pro-rata) ${fmtBaht(res.premiumDelta)} — ${res.paymentNo}`
                          : `คืนเบี้ย (pro-rata) ${fmtBaht(Math.abs(res.premiumDelta))} — ${res.paymentNo}`,
                  });
                  setCoverageOpen(false);
                } catch (e) {
                  toast.error(apiError(e));
                }
              }}
            >
              {coverageEndorsing ? 'กำลังบันทึก…' : 'บันทึก'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}

/** Placeholder shown while the policy detail is loading — mirrors the real layout. */
function PolicyDetailSkeleton() {
  return (
    <div className="space-y-6">
      <Skeleton className="h-4 w-16" />

      <div className="flex flex-wrap items-start justify-between gap-4">
        <div className="space-y-2">
          <div className="flex items-center gap-3">
            <Skeleton className="h-8 w-48" />
            <Skeleton className="h-5 w-16 rounded-full" />
          </div>
          <Skeleton className="h-4 w-56" />
        </div>
        <div className="flex flex-wrap gap-2">
          <Skeleton className="h-9 w-28" />
          <Skeleton className="h-9 w-24" />
          <Skeleton className="h-9 w-24" />
        </div>
      </div>

      <div className="grid gap-4 sm:grid-cols-4">
        {Array.from({ length: 4 }).map((_, i) => (
          <Card key={i}>
            <CardContent className="space-y-2 p-4">
              <Skeleton className="h-3 w-20" />
              <Skeleton className="h-5 w-24" />
            </CardContent>
          </Card>
        ))}
      </div>

      {Array.from({ length: 2 }).map((_, i) => (
        <Card key={i}>
          <CardHeader>
            <Skeleton className="h-5 w-32" />
          </CardHeader>
          <CardContent className="space-y-3 pt-0">
            {Array.from({ length: 3 }).map((_, r) => (
              <Skeleton key={r} className="h-4 w-full" />
            ))}
          </CardContent>
        </Card>
      ))}
    </div>
  );
}
