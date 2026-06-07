'use client';

import { useState } from 'react';
import { toast } from 'sonner';
import { SlidersHorizontal, Pencil, Plus, Trash2 } from 'lucide-react';
import {
  useGetPremiumRatesQuery,
  useUpdatePremiumRateMutation,
  useCreatePremiumRateMutation,
  useGetAgeLoadingBandsQuery,
  useCreateAgeBandMutation,
  useUpdateAgeBandMutation,
  useDeleteAgeBandMutation,
  useGetRatingSettingsQuery,
  useUpdateRatingSettingMutation,
  type PremiumRateDto,
  type AgeLoadingBandDto,
  type CoverageType,
} from '@/lib/api/insuranceApi';
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
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Skeleton } from '@/components/ui/skeleton';
import { Can } from '@/components/can';
import { P } from '@/lib/auth/permissions';
import { apiError, fmtDate } from '@/lib/utils';

const COVERAGE_LABEL: Record<CoverageType, string> = {
  Type1: 'ประกันชั้น 1',
  Type2Plus: 'ประกันชั้น 2+',
  Type3Plus: 'ประกันชั้น 3+',
  Type3: 'ประกันชั้น 3',
};

const SETTING_LABEL: Record<string, string> = {
  DEDUCTIBLE_RELIEF_RATE: 'สัดส่วนส่วนลดค่าเสียหายส่วนแรก',
  DEDUCTIBLE_RELIEF_CAP: 'เพดานส่วนลด (เทียบเบี้ยฐาน)',
};

const today = () => new Date().toISOString().slice(0, 10);
const pct = (v: number) => `${(v * 100).toLocaleString('th-TH', { maximumFractionDigits: 2 })}%`;

export default function PremiumRatesPage() {
  return (
    <div className="space-y-6">
      <div className="flex items-center gap-3">
        <span className="flex h-10 w-10 shrink-0 items-center justify-center rounded-lg bg-primary/10 text-primary">
          <SlidersHorizontal className="h-5 w-5" />
        </span>
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">พิกัดอัตราเบี้ย</h1>
          <p className="text-sm text-muted-foreground">
            ปรับอัตราเบี้ยฐาน โหลดตามอายุรถ และส่วนลดค่าเสียหายส่วนแรก ได้โดยไม่ต้อง deploy
          </p>
        </div>
      </div>

      <CoverageRatesCard />
      <AgeBandsCard />
      <DeductibleSettingsCard />
    </div>
  );
}

/** Section 1 — effective-dated base rate per coverage type. */
function CoverageRatesCard() {
  const { data, isLoading } = useGetPremiumRatesQuery();
  const [update] = useUpdatePremiumRateMutation();
  const [create] = useCreatePremiumRateMutation();
  const [edit, setEdit] = useState<{ row: PremiumRateDto; rate: string } | null>(null);
  const [add, setAdd] = useState<{ coverage: CoverageType; rate: string; effectiveDate: string } | null>(null);
  const [busy, setBusy] = useState(false);

  const run = async (fn: () => Promise<unknown>, done: () => void) => {
    setBusy(true);
    try {
      await fn();
      toast.success('บันทึกแล้ว');
      done();
    } catch (e) {
      toast.error(apiError(e));
    } finally {
      setBusy(false);
    }
  };

  return (
    <Card>
      <CardHeader className="flex-row items-center justify-between">
        <CardTitle className="text-base">อัตราเบี้ยฐานต่อชั้น (เบี้ยฐาน = ทุนประกัน × อัตรา)</CardTitle>
        <Can permission={P.RatingManage}>
          <Button size="sm" variant="outline" onClick={() => setAdd({ coverage: 'Type1', rate: '', effectiveDate: today() })}>
            <Plus /> เพิ่มเวอร์ชันอัตรา
          </Button>
        </Can>
      </CardHeader>
      <CardContent className="pt-0">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>ชั้นความคุ้มครอง</TableHead>
              <TableHead className="text-right">อัตรา</TableHead>
              <TableHead className="text-right">คิดเป็น %</TableHead>
              <TableHead>มีผลตั้งแต่</TableHead>
              <TableHead className="text-right">จัดการ</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {isLoading && (
              <TableRow>
                <TableCell colSpan={5}>
                  <Skeleton className="h-5 w-full" />
                </TableCell>
              </TableRow>
            )}
            {data?.map((r) => (
              <TableRow key={r.id}>
                <TableCell className="font-medium">{COVERAGE_LABEL[r.coverage] ?? r.coverage}</TableCell>
                <TableCell className="text-right tabular-nums">{r.rate.toFixed(4)}</TableCell>
                <TableCell className="text-right tabular-nums">{pct(r.rate)}</TableCell>
                <TableCell>{fmtDate(r.effectiveDate)}</TableCell>
                <TableCell className="text-right">
                  <Can permission={P.RatingManage}>
                    <Button size="sm" variant="ghost" aria-label="แก้ไข" onClick={() => setEdit({ row: r, rate: String(r.rate) })}>
                      <Pencil />
                    </Button>
                  </Can>
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </CardContent>

      {/* Edit rate value */}
      <Dialog open={!!edit} onOpenChange={(o) => !o && setEdit(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>แก้ไขอัตราเบี้ย</DialogTitle>
            <DialogDescription>{edit && (COVERAGE_LABEL[edit.row.coverage] ?? edit.row.coverage)}</DialogDescription>
          </DialogHeader>
          <div className="space-y-2">
            <Label required>อัตรา (เช่น 0.045 = 4.5%)</Label>
            <Input
              type="number"
              step="0.0001"
              value={edit?.rate ?? ''}
              onChange={(e) => setEdit((s) => (s ? { ...s, rate: e.target.value } : s))}
            />
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setEdit(null)}>
              ยกเลิก
            </Button>
            <Button
              disabled={busy}
              onClick={() => edit && run(() => update({ id: edit.row.id, rate: Number(edit.rate) }).unwrap(), () => setEdit(null))}
            >
              บันทึก
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Add effective-dated version */}
      <Dialog open={!!add} onOpenChange={(o) => !o && setAdd(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>เพิ่มเวอร์ชันอัตราใหม่</DialogTitle>
            <DialogDescription>กำหนดอัตราใหม่ที่จะมีผลตั้งแต่วันที่ระบุ (ใบเสนอราคาเก่ายังใช้อัตราเดิม)</DialogDescription>
          </DialogHeader>
          <div className="space-y-4">
            <div className="space-y-2">
              <Label required>ชั้นความคุ้มครอง</Label>
              <Select value={add?.coverage} onValueChange={(v) => setAdd((s) => (s ? { ...s, coverage: v as CoverageType } : s))}>
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {(Object.keys(COVERAGE_LABEL) as CoverageType[]).map((c) => (
                    <SelectItem key={c} value={c}>
                      {COVERAGE_LABEL[c]}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-2">
                <Label required>อัตรา</Label>
                <Input
                  type="number"
                  step="0.0001"
                  value={add?.rate ?? ''}
                  onChange={(e) => setAdd((s) => (s ? { ...s, rate: e.target.value } : s))}
                  placeholder="0.045"
                />
              </div>
              <div className="space-y-2">
                <Label required>มีผลตั้งแต่</Label>
                <Input
                  type="date"
                  value={add?.effectiveDate ?? ''}
                  onChange={(e) => setAdd((s) => (s ? { ...s, effectiveDate: e.target.value } : s))}
                />
              </div>
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setAdd(null)}>
              ยกเลิก
            </Button>
            <Button
              disabled={busy || !add?.rate || !add?.effectiveDate}
              onClick={() =>
                add &&
                run(
                  () => create({ coverage: add.coverage, rate: Number(add.rate), effectiveDate: add.effectiveDate }).unwrap(),
                  () => setAdd(null),
                )
              }
            >
              เพิ่ม
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </Card>
  );
}

/** Section 2 — vehicle-age loading bands. */
function AgeBandsCard() {
  const { data, isLoading } = useGetAgeLoadingBandsQuery();
  const [create] = useCreateAgeBandMutation();
  const [update] = useUpdateAgeBandMutation();
  const [remove] = useDeleteAgeBandMutation();
  const [edit, setEdit] = useState<{ row: AgeLoadingBandDto; maxAge: string; surcharge: string } | null>(null);
  const [add, setAdd] = useState<{ maxAge: string; surcharge: string; effectiveDate: string } | null>(null);
  const [busy, setBusy] = useState(false);

  const run = async (fn: () => Promise<unknown>, done: () => void) => {
    setBusy(true);
    try {
      await fn();
      toast.success('บันทึกแล้ว');
      done();
    } catch (e) {
      toast.error(apiError(e));
    } finally {
      setBusy(false);
    }
  };

  const maxAgeLabel = (v: number | null) => (v === null ? 'มากกว่านั้น' : `≤ ${v} ปี`);

  return (
    <Card>
      <CardHeader className="flex-row items-center justify-between">
        <CardTitle className="text-base">โหลดตามอายุรถ</CardTitle>
        <Can permission={P.RatingManage}>
          <Button size="sm" variant="outline" onClick={() => setAdd({ maxAge: '', surcharge: '', effectiveDate: today() })}>
            <Plus /> เพิ่มช่วง
          </Button>
        </Can>
      </CardHeader>
      <CardContent className="pt-0">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>ช่วงอายุรถ</TableHead>
              <TableHead className="text-right">โหลด</TableHead>
              <TableHead>มีผลตั้งแต่</TableHead>
              <TableHead className="text-right">จัดการ</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {isLoading && (
              <TableRow>
                <TableCell colSpan={4}>
                  <Skeleton className="h-5 w-full" />
                </TableCell>
              </TableRow>
            )}
            {data?.map((b) => (
              <TableRow key={b.id}>
                <TableCell className="font-medium">{maxAgeLabel(b.maxAge)}</TableCell>
                <TableCell className="text-right tabular-nums">{pct(b.surcharge)}</TableCell>
                <TableCell>{fmtDate(b.effectiveDate)}</TableCell>
                <TableCell className="text-right">
                  <Can permission={P.RatingManage}>
                    <div className="flex justify-end gap-1">
                      <Button
                        size="sm"
                        variant="ghost"
                        aria-label="แก้ไข"
                        onClick={() => setEdit({ row: b, maxAge: b.maxAge === null ? '' : String(b.maxAge), surcharge: String(b.surcharge) })}
                      >
                        <Pencil />
                      </Button>
                      <Button
                        size="sm"
                        variant="ghost"
                        aria-label="ลบ"
                        disabled={busy}
                        onClick={() => run(() => remove(b.id).unwrap(), () => {})}
                      >
                        <Trash2 className="text-destructive" />
                      </Button>
                    </div>
                  </Can>
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </CardContent>

      {/* Edit band */}
      <Dialog open={!!edit} onOpenChange={(o) => !o && setEdit(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>แก้ไขช่วงอายุรถ</DialogTitle>
            <DialogDescription>ปล่อยช่อง “อายุไม่เกิน” ว่างไว้สำหรับช่วงปลายเปิด (มากกว่านั้น)</DialogDescription>
          </DialogHeader>
          <div className="grid grid-cols-2 gap-4">
            <div className="space-y-2">
              <Label>อายุไม่เกิน (ปี)</Label>
              <Input
                type="number"
                value={edit?.maxAge ?? ''}
                onChange={(e) => setEdit((s) => (s ? { ...s, maxAge: e.target.value } : s))}
                placeholder="เปิด"
              />
            </div>
            <div className="space-y-2">
              <Label required>โหลด (เช่น 0.05 = 5%)</Label>
              <Input
                type="number"
                step="0.0001"
                value={edit?.surcharge ?? ''}
                onChange={(e) => setEdit((s) => (s ? { ...s, surcharge: e.target.value } : s))}
              />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setEdit(null)}>
              ยกเลิก
            </Button>
            <Button
              disabled={busy}
              onClick={() =>
                edit &&
                run(
                  () =>
                    update({
                      id: edit.row.id,
                      maxAge: edit.maxAge.trim() === '' ? null : Number(edit.maxAge),
                      surcharge: Number(edit.surcharge),
                    }).unwrap(),
                  () => setEdit(null),
                )
              }
            >
              บันทึก
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Add band */}
      <Dialog open={!!add} onOpenChange={(o) => !o && setAdd(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>เพิ่มช่วงอายุรถ</DialogTitle>
          </DialogHeader>
          <div className="space-y-4">
            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-2">
                <Label>อายุไม่เกิน (ปี)</Label>
                <Input
                  type="number"
                  value={add?.maxAge ?? ''}
                  onChange={(e) => setAdd((s) => (s ? { ...s, maxAge: e.target.value } : s))}
                  placeholder="เปิด"
                />
              </div>
              <div className="space-y-2">
                <Label required>โหลด</Label>
                <Input
                  type="number"
                  step="0.0001"
                  value={add?.surcharge ?? ''}
                  onChange={(e) => setAdd((s) => (s ? { ...s, surcharge: e.target.value } : s))}
                  placeholder="0.05"
                />
              </div>
            </div>
            <div className="space-y-2">
              <Label required>มีผลตั้งแต่</Label>
              <Input
                type="date"
                value={add?.effectiveDate ?? ''}
                onChange={(e) => setAdd((s) => (s ? { ...s, effectiveDate: e.target.value } : s))}
              />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setAdd(null)}>
              ยกเลิก
            </Button>
            <Button
              disabled={busy || !add?.surcharge || !add?.effectiveDate}
              onClick={() =>
                add &&
                run(
                  () =>
                    create({
                      maxAge: add.maxAge.trim() === '' ? null : Number(add.maxAge),
                      surcharge: Number(add.surcharge),
                      effectiveDate: add.effectiveDate,
                    }).unwrap(),
                  () => setAdd(null),
                )
              }
            >
              เพิ่ม
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </Card>
  );
}

/** Section 3 — deductible-relief settings. */
function DeductibleSettingsCard() {
  const { data, isLoading } = useGetRatingSettingsQuery();
  const [update] = useUpdateRatingSettingMutation();
  const [edit, setEdit] = useState<{ code: string; value: string } | null>(null);
  const [busy, setBusy] = useState(false);

  const save = async () => {
    if (!edit) return;
    setBusy(true);
    try {
      await update({ code: edit.code, value: Number(edit.value) }).unwrap();
      toast.success('บันทึกแล้ว');
      setEdit(null);
    } catch (e) {
      toast.error(apiError(e));
    } finally {
      setBusy(false);
    }
  };

  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-base">ส่วนลดค่าเสียหายส่วนแรก</CardTitle>
      </CardHeader>
      <CardContent className="pt-0">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>รายการ</TableHead>
              <TableHead className="text-right">ค่า</TableHead>
              <TableHead className="text-right">จัดการ</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {isLoading && (
              <TableRow>
                <TableCell colSpan={3}>
                  <Skeleton className="h-5 w-full" />
                </TableCell>
              </TableRow>
            )}
            {data?.map((s) => (
              <TableRow key={s.code}>
                <TableCell className="font-medium">{SETTING_LABEL[s.code] ?? s.code}</TableCell>
                <TableCell className="text-right tabular-nums">{s.value.toFixed(4)}</TableCell>
                <TableCell className="text-right">
                  <Can permission={P.RatingManage}>
                    <Button size="sm" variant="ghost" aria-label="แก้ไข" onClick={() => setEdit({ code: s.code, value: String(s.value) })}>
                      <Pencil />
                    </Button>
                  </Can>
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </CardContent>

      <Dialog open={!!edit} onOpenChange={(o) => !o && setEdit(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>แก้ไขค่า</DialogTitle>
            <DialogDescription>{edit && (SETTING_LABEL[edit.code] ?? edit.code)}</DialogDescription>
          </DialogHeader>
          <div className="space-y-2">
            <Label required>ค่า (สัดส่วน 0–1)</Label>
            <Input
              type="number"
              step="0.0001"
              value={edit?.value ?? ''}
              onChange={(e) => setEdit((s) => (s ? { ...s, value: e.target.value } : s))}
            />
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setEdit(null)}>
              ยกเลิก
            </Button>
            <Button disabled={busy} onClick={save}>
              บันทึก
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </Card>
  );
}
