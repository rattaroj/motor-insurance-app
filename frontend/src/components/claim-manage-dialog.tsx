'use client';

import { useEffect, useRef, useState } from 'react';
import { toast } from 'sonner';
import { Upload, Wrench, FileText } from 'lucide-react';
import {
  useGetClaimQuery,
  useGetGaragesQuery,
  useAssignClaimMutation,
  useUploadClaimPhotoMutation,
  useGetClaimLetterMutation,
  fileUrl,
} from '@/lib/api/insuranceApi';
import { Button } from '@/components/ui/button';
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Skeleton } from '@/components/ui/skeleton';
import { ImageGallery } from '@/components/image-preview';
import { Can } from '@/components/can';
import { P } from '@/lib/auth/permissions';
import { apiError, saveUrl } from '@/lib/utils';

const DECIDED = ['Approved', 'Rejected', 'Paid', 'Closed'];

const NONE = 'none';

/** Manage a claim's repair shop, surveyor and damage photos. */
export function ClaimManageDialog({ claimId, onClose }: { claimId: number | null; onClose: () => void }) {
  const open = claimId !== null;
  const { data: claim, isFetching } = useGetClaimQuery(claimId as number, { skip: claimId === null });
  const { data: garages } = useGetGaragesQuery();
  const [assign, { isLoading: assigning }] = useAssignClaimMutation();
  const [uploadPhoto, { isLoading: uploading }] = useUploadClaimPhotoMutation();
  const [getLetter, { isLoading: lettering }] = useGetClaimLetterMutation();

  const [garageId, setGarageId] = useState(NONE);
  const [surveyor, setSurveyor] = useState('');
  const fileRef = useRef<HTMLInputElement>(null);

  useEffect(() => {
    if (claim) {
      setGarageId(claim.garageId ? String(claim.garageId) : NONE);
      setSurveyor(claim.surveyorName ?? '');
    }
  }, [claim]);

  const saveAssign = async () => {
    if (claimId === null) return;
    try {
      await assign({
        id: claimId,
        garageId: garageId === NONE ? null : Number(garageId),
        surveyorName: surveyor.trim() || null,
      }).unwrap();
      toast.success('บันทึกการมอบหมายแล้ว');
    } catch (e) {
      toast.error(apiError(e));
    }
  };

  const onPickFile = async (file: File) => {
    if (claimId === null) return;
    try {
      await uploadPhoto({ id: claimId, file }).unwrap();
      toast.success('อัปโหลดรูปแล้ว');
    } catch (e) {
      toast.error(apiError(e));
    }
  };

  const downloadLetter = async () => {
    if (claimId === null) return;
    try {
      const url = await getLetter(claimId).unwrap();
      saveUrl(url, `${claim?.claimNo ?? 'claim'}-letter.pdf`);
    } catch (e) {
      toast.error(apiError(e));
    }
  };

  return (
    <Dialog open={open} onOpenChange={(o) => !o && onClose()}>
      <DialogContent className="max-w-lg">
        <DialogHeader>
          <DialogTitle>จัดการเคลม {claim?.claimNo ?? ''}</DialogTitle>
          <DialogDescription>มอบหมายอู่/ผู้สำรวจภัย และแนบรูปความเสียหาย</DialogDescription>
        </DialogHeader>

        {isFetching || !claim ? (
          <div className="space-y-3">
            <Skeleton className="h-10 w-full" />
            <Skeleton className="h-24 w-full" />
          </div>
        ) : (
          <div className="space-y-5">
            {/* Assign garage + surveyor */}
            <div className="grid grid-cols-2 gap-3">
              <div className="space-y-2">
                <Label>อู่/ศูนย์ซ่อม</Label>
                <Select value={garageId} onValueChange={setGarageId}>
                  <SelectTrigger>
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value={NONE}>— ไม่ระบุ —</SelectItem>
                    {(garages ?? []).map((g) => (
                      <SelectItem key={g.id} value={String(g.id)}>
                        {g.name}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div className="space-y-2">
                <Label htmlFor="surveyor">ผู้สำรวจภัย</Label>
                <Input id="surveyor" value={surveyor} onChange={(e) => setSurveyor(e.target.value)} placeholder="ชื่อผู้สำรวจ" />
              </div>
            </div>
            <Can permission={P.ClaimReview}>
              <Button size="sm" variant="outline" disabled={assigning} onClick={saveAssign}>
                <Wrench /> บันทึกการมอบหมาย
              </Button>
            </Can>

            {/* Damage photos */}
            <div className="space-y-2">
              <div className="flex items-center justify-between">
                <Label>รูปความเสียหาย ({claim.photos.length})</Label>
                <Can permission={P.ClaimReview}>
                  <Button size="sm" variant="outline" disabled={uploading} onClick={() => fileRef.current?.click()}>
                    {uploading ? 'กำลังอัปโหลด…' : (<><Upload /> เพิ่มรูป</>)}
                  </Button>
                </Can>
                <input
                  ref={fileRef}
                  type="file"
                  accept="image/jpeg,image/png"
                  className="hidden"
                  onChange={(e) => {
                    const f = e.target.files?.[0];
                    if (f) onPickFile(f);
                    e.target.value = '';
                  }}
                />
              </div>
              {claim.photos.length === 0 ? (
                <p className="py-4 text-center text-sm text-muted-foreground">ยังไม่มีรูปความเสียหาย</p>
              ) : (
                <ImageGallery
                  items={claim.photos.map((p, i) => ({
                    src: fileUrl(p.imagePath),
                    alt: `ความเสียหาย ${i + 1}`,
                    title: `รูปที่ ${i + 1}`,
                  }))}
                />
              )}
            </div>

            {/* Settlement letter (decided claims only) */}
            {DECIDED.includes(claim.status) && (
              <div className="border-t pt-4">
                <Button variant="outline" size="sm" disabled={lettering} onClick={downloadLetter}>
                  <FileText /> {lettering ? 'กำลังสร้าง…' : 'จดหมายแจ้งผลสินไหม (PDF)'}
                </Button>
              </div>
            )}
          </div>
        )}
      </DialogContent>
    </Dialog>
  );
}
