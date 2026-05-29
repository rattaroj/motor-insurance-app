'use client';

import { use, useState } from 'react';
import Link from 'next/link';
import { toast } from 'sonner';
import { ArrowLeft, CheckCircle, XCircle, RefreshCw, Plus, Wallet } from 'lucide-react';
import {
  useGetPolicyQuery,
  useGetPolicyHistoryQuery,
  useGetPaymentsQuery,
  useGetClaimsQuery,
  useActivatePolicyMutation,
  useCancelPolicyMutation,
  useRenewPolicyMutation,
  useSettlePaymentMutation,
  useFileClaimMutation,
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
import { Separator } from '@/components/ui/separator';
import { Skeleton } from '@/components/ui/skeleton';
import { Can } from '@/components/can';
import { P } from '@/lib/auth/permissions';
import { apiError, fmtBaht, fmtDate, fmtDateTime } from '@/lib/utils';

const today = () => new Date().toISOString().slice(0, 10);

export default function PolicyDetailPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params);
  const policyId = Number(id);

  const { data: policy, isLoading } = useGetPolicyQuery(policyId);
  const { data: history } = useGetPolicyHistoryQuery(policyId);
  const { data: payments } = useGetPaymentsQuery({ policyId, pageSize: 100 });
  const { data: claims } = useGetClaimsQuery({ policyId, pageSize: 100 });

  const [activate, { isLoading: activating }] = useActivatePolicyMutation();
  const [cancel, { isLoading: cancelling }] = useCancelPolicyMutation();
  const [renew, { isLoading: renewing }] = useRenewPolicyMutation();
  const [settle, { isLoading: settling }] = useSettlePaymentMutation();
  const [fileClaim, { isLoading: filing }] = useFileClaimMutation();

  const [cancelOpen, setCancelOpen] = useState(false);
  const [reason, setReason] = useState('');
  const [settleFor, setSettleFor] = useState<number | null>(null);
  const [referenceNo, setReferenceNo] = useState('');
  const [claimOpen, setClaimOpen] = useState(false);
  const [claimForm, setClaimForm] = useState({ incidentDate: today(), description: '', claimedAmount: '' });

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
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setCancelOpen(false)}>
              ปิด
            </Button>
            <Button
              variant="destructive"
              disabled={cancelling || !reason}
              onClick={async () => {
                const ok = await run(() => cancel({ id: policyId, reason }).unwrap(), 'ยกเลิกกรมธรรม์แล้ว');
                if (ok) setCancelOpen(false);
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
