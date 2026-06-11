'use client';

import { use, useState } from 'react';
import Link from 'next/link';
import { toast } from 'sonner';
import { ArrowLeft, FileWarning, FileDown, Wrench, AlertTriangle, Info, Phone } from 'lucide-react';
import {
  useGetClaimQuery,
  useGetClaimHistoryQuery,
  useGetClaimLetterMutation,
  fileUrl,
} from '@/lib/api/insuranceApi';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Skeleton } from '@/components/ui/skeleton';
import { Can } from '@/components/can';
import { PageHeader } from '@/components/page-header';
import { AuditFooter } from '@/components/audit-footer';
import { StatusBadge } from '@/components/StatusBadge';
import { ImageGallery } from '@/components/image-preview';
import { ClaimManageDialog } from '@/components/claim-manage-dialog';
import { P } from '@/lib/auth/permissions';
import { apiError, cn, fmtBaht, fmtDate, fmtDateTime, saveUrl } from '@/lib/utils';

function Fact({ label, value }: { label: string; value: React.ReactNode }) {
  return (
    <Card>
      <CardContent className="p-4">
        <p className="text-xs text-muted-foreground">{label}</p>
        <div className="mt-1 font-medium">{value}</div>
      </CardContent>
    </Card>
  );
}

export default function ClaimDetailPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params);
  const claimId = Number(id);
  const { data: claim, isLoading } = useGetClaimQuery(claimId);
  const { data: history } = useGetClaimHistoryQuery(claimId);
  const [getLetter, { isLoading: lettering }] = useGetClaimLetterMutation();
  const [manageId, setManageId] = useState<number | null>(null);

  const downloadLetter = async () => {
    if (!claim) return;
    try {
      const url = await getLetter(claim.id).unwrap();
      saveUrl(url, `${claim.claimNo}-letter.pdf`);
    } catch (e) {
      toast.error(apiError(e));
    }
  };

  if (isLoading) {
    return (
      <div className="space-y-6">
        <Skeleton className="h-6 w-24" />
        <Skeleton className="h-24 w-full" />
        <div className="grid gap-4 sm:grid-cols-4">
          {Array.from({ length: 4 }).map((_, i) => (
            <Skeleton key={i} className="h-20" />
          ))}
        </div>
      </div>
    );
  }
  if (!claim) return <p className="text-sm text-destructive">ไม่พบเคลม</p>;

  // Newest first; merge contiguous rows with the same status so the timeline
  // shows status changes, not every row-version (e.g. assign/photo edits).
  const timeline = (history ?? [])
    .filter((h, i, all) => i === 0 || h.status !== all[i - 1].status)
    .reverse();

  return (
    <div className="space-y-6">
      <Link href="/claims" className="inline-flex items-center gap-1 text-sm text-muted-foreground hover:text-foreground">
        <ArrowLeft className="h-4 w-4" /> กลับ
      </Link>

      <PageHeader
        icon={FileWarning}
        title={claim.claimNo}
        badge={<StatusBadge status={claim.status} />}
        description={
          <>
            กรมธรรม์{' '}
            <Link href={`/policies/${claim.policyId}`} className="font-medium text-primary hover:underline">
              {claim.policyNo}
            </Link>{' '}
            · เกิดเหตุ {fmtDate(claim.incidentDate)}
          </>
        }
        actions={
          <>
            <Button variant="outline" disabled={lettering} onClick={downloadLetter}>
              <FileDown /> {lettering ? 'กำลังสร้าง…' : 'จดหมายแจ้งผล'}
            </Button>
            <Can permission={P.ClaimReview}>
              <Button onClick={() => setManageId(claim.id)}>
                <Wrench /> จัดการเคลม
              </Button>
            </Can>
          </>
        }
      />

      {/* Risk flags */}
      {claim.riskFlags.length > 0 && (
        <div className="space-y-2">
          {claim.riskFlags.map((f) => (
            <div
              key={f.code}
              className={cn(
                'flex items-center gap-2 rounded-lg border px-3 py-2 text-sm',
                f.severity === 'warn'
                  ? 'border-amber-500/30 bg-amber-500/10 text-amber-700 dark:text-amber-400'
                  : 'border-sky-500/30 bg-sky-500/10 text-sky-700 dark:text-sky-400',
              )}
            >
              {f.severity === 'warn' ? <AlertTriangle className="h-4 w-4 shrink-0" /> : <Info className="h-4 w-4 shrink-0" />}
              {f.label}
            </div>
          ))}
        </div>
      )}

      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <Fact label="ยอดเรียกร้อง" value={<span className="tabular-nums">{fmtBaht(claim.claimedAmount)}</span>} />
        <Fact
          label="ยอดอนุมัติ"
          value={
            claim.approvedAmount != null ? (
              <span className="tabular-nums text-emerald-600 dark:text-emerald-400">{fmtBaht(claim.approvedAmount)}</span>
            ) : (
              <span className="text-muted-foreground">—</span>
            )
          }
        />
        <Fact
          label="อู่/ศูนย์ซ่อม"
          value={
            claim.garageName ? (
              <span className="inline-flex flex-wrap items-center gap-x-2">
                {claim.garageName}
                {claim.garagePhone && (
                  <span className="inline-flex items-center gap-1 text-sm text-muted-foreground">
                    <Phone className="h-3.5 w-3.5" /> {claim.garagePhone}
                  </span>
                )}
              </span>
            ) : (
              <span className="text-muted-foreground">ยังไม่ระบุ</span>
            )
          }
        />
        <Fact
          label="เจ้าหน้าที่สำรวจ"
          value={claim.surveyorName ?? <span className="text-muted-foreground">ยังไม่ระบุ</span>}
        />
      </div>

      {(claim.description || claim.rejectReason) && (
        <Card>
          <CardHeader>
            <CardTitle className="text-base">รายละเอียดเหตุการณ์</CardTitle>
          </CardHeader>
          <CardContent className="space-y-3 text-sm">
            {claim.description && <p>{claim.description}</p>}
            {claim.rejectReason && (
              <p className="rounded-lg border border-red-500/30 bg-red-500/10 px-3 py-2 text-red-700 dark:text-red-400">
                เหตุผลที่ปฏิเสธ: {claim.rejectReason}
              </p>
            )}
          </CardContent>
        </Card>
      )}

      {/* Damage photos */}
      <Card>
        <CardHeader>
          <CardTitle className="text-base">รูปความเสียหาย ({claim.photos.length})</CardTitle>
        </CardHeader>
        <CardContent>
          {claim.photos.length === 0 ? (
            <p className="py-4 text-center text-sm text-muted-foreground">ยังไม่มีรูป — อัปโหลดได้จากปุ่ม “จัดการเคลม”</p>
          ) : (
            <ImageGallery
              items={claim.photos.map((p, i) => ({
                src: fileUrl(p.imagePath),
                alt: `รูปความเสียหาย ${i + 1}`,
                title: `รูปที่ ${i + 1}`,
                subtitle: fmtDateTime(p.createdAt),
              }))}
            />
          )}
        </CardContent>
      </Card>

      {/* Status timeline (temporal history) */}
      <Card>
        <CardHeader>
          <CardTitle className="text-base">ไทม์ไลน์สถานะ</CardTitle>
        </CardHeader>
        <CardContent>
          {timeline.length === 0 ? (
            <p className="py-4 text-center text-sm text-muted-foreground">ไม่มีประวัติ</p>
          ) : (
            <ol className="relative space-y-5 border-l border-border pl-5">
              {timeline.map((h, i) => (
                <li key={`${h.status}-${h.validFrom}`} className="relative">
                  <span
                    aria-hidden
                    className={cn(
                      'absolute -left-[26.5px] top-1 h-3 w-3 rounded-full border-2 border-background',
                      i === 0 ? 'bg-primary' : 'bg-muted-foreground/40',
                    )}
                  />
                  <div className="flex flex-wrap items-center gap-x-3 gap-y-1">
                    <StatusBadge status={h.status} />
                    <span className="text-xs text-muted-foreground">{fmtDateTime(h.validFrom)}</span>
                    {h.approvedAmount != null && (
                      <span className="text-xs tabular-nums text-muted-foreground">
                        อนุมัติ {fmtBaht(h.approvedAmount)}
                      </span>
                    )}
                  </div>
                </li>
              ))}
            </ol>
          )}
        </CardContent>
      </Card>

      <AuditFooter audit={claim.audit} />

      <ClaimManageDialog claimId={manageId} onClose={() => setManageId(null)} />
    </div>
  );
}
