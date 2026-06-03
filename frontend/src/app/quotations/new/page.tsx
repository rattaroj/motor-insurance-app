'use client';

import { useEffect, useState } from 'react';
import { useRouter } from 'next/navigation';
import Link from 'next/link';
import { toast } from 'sonner';
import { ArrowLeft, Plus, Trash2, Upload, Check } from 'lucide-react';
import {
  useCreateQuotationMutation,
  usePreviewPremiumMutation,
  useGetCustomersQuery,
  useGetVehiclesQuery,
  useGetRidersQuery,
  useUploadIdCardMutation,
  fileUrl,
  NCB_STEPS,
  type CoverageType,
  type DriverInput,
} from '@/lib/api/insuranceApi';
import { ImagePreview } from '@/components/image-preview';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { apiError, cn, fmtBaht } from '@/lib/utils';
import { useDebouncedValue } from '@/lib/use-debounced';

const COVERAGES: { value: CoverageType; label: string }[] = [
  { value: 'Type1', label: 'ชั้น 1' },
  { value: 'Type2Plus', label: 'ชั้น 2+' },
  { value: 'Type3Plus', label: 'ชั้น 3+' },
  { value: 'Type3', label: 'ชั้น 3' },
];

const DEDUCTIBLES = [0, 1000, 2000, 5000];

const emptyCreate = {
  customerId: '', vehicleId: '', coverageType: '' as CoverageType | '', sumInsured: '',
  ncbPercent: '0', deductible: '0',
};
const MAX_DRIVERS = 5;

/** A driver row in the create form: the API payload plus UI-only upload state. */
type DriverRow = DriverInput & { fileName: string; uploading: boolean };
const emptyDriver = (): DriverRow => ({ fullName: '', nationalId: '', idCardImagePath: '', fileName: '', uploading: false });
const driverComplete = (d: DriverRow) =>
  d.fullName.trim() !== '' && d.nationalId.length === 13 && d.idCardImagePath !== '';

/** One line in the premium breakdown panel. */
function Row({ label, value, negative }: { label: string; value: string; negative?: boolean }) {
  return (
    <div className="flex items-center justify-between">
      <dt className="text-muted-foreground">{label}</dt>
      <dd className={cn('tabular-nums', negative && 'text-emerald-600')}>{value}</dd>
    </div>
  );
}

export default function NewQuotationPage() {
  const router = useRouter();
  const { data: customers } = useGetCustomersQuery({ pageSize: 100 });
  const [createQuotation, { isLoading: creating }] = useCreateQuotationMutation();
  const [uploadIdCard] = useUploadIdCardMutation();

  const [form, setForm] = useState(emptyCreate);
  const [riderIds, setRiderIds] = useState<number[]>([]);
  const [drivers, setDrivers] = useState<DriverRow[]>([emptyDriver()]);

  const { data: riders } = useGetRidersQuery();
  const [previewPremium, { data: breakdown, isLoading: previewing }] = usePreviewPremiumMutation();

  const toggleRider = (id: number) =>
    setRiderIds((ids) => (ids.includes(id) ? ids.filter((x) => x !== id) : [...ids, id]));

  // Live premium preview: re-rate (debounced) whenever a pricing input changes.
  const canPreview = !!form.vehicleId && !!form.coverageType && Number(form.sumInsured) > 0;
  const previewKey = useDebouncedValue(
    JSON.stringify({
      v: form.vehicleId, c: form.coverageType, s: form.sumInsured,
      n: form.ncbPercent, d: form.deductible, r: riderIds,
    }),
    400,
  );
  useEffect(() => {
    if (!canPreview) return;
    previewPremium({
      vehicleId: Number(form.vehicleId),
      coverageType: form.coverageType as CoverageType,
      sumInsured: Number(form.sumInsured),
      ncbPercent: Number(form.ncbPercent),
      deductible: Number(form.deductible),
      riderIds,
    });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [previewKey]);

  const setDriver = (i: number, patch: Partial<DriverRow>) =>
    setDrivers((ds) => ds.map((d, idx) => (idx === i ? { ...d, ...patch } : d)));

  const uploadDriverImage = async (i: number, file: File) => {
    setDriver(i, { uploading: true, fileName: file.name });
    try {
      const res = await uploadIdCard(file).unwrap();
      setDriver(i, { idCardImagePath: res.path, uploading: false });
    } catch (e) {
      setDriver(i, { uploading: false, fileName: '' });
      toast.error(apiError(e));
    }
  };

  const { data: vehicles } = useGetVehiclesQuery(
    { customerId: Number(form.customerId), pageSize: 100 },
    { skip: !form.customerId },
  );
  const vehiclesOfCustomer = vehicles?.items ?? [];

  const driversValid = drivers.length >= 1 && drivers.length <= MAX_DRIVERS && drivers.every(driverComplete);

  const submit = async () => {
    try {
      const res = await createQuotation({
        customerId: Number(form.customerId),
        vehicleId: Number(form.vehicleId),
        coverageType: form.coverageType as CoverageType,
        sumInsured: Number(form.sumInsured),
        ncbPercent: Number(form.ncbPercent),
        deductible: Number(form.deductible),
        riderIds,
        drivers: drivers.map((d) => ({
          fullName: d.fullName,
          nationalId: d.nationalId,
          idCardImagePath: d.idCardImagePath,
        })),
      }).unwrap();
      toast.success('สร้างใบเสนอราคาแล้ว', { description: `ใบเสนอราคา #${res.id}` });
      router.push('/quotations');
    } catch (e) {
      toast.error(apiError(e));
    }
  };

  return (
    <div className="mx-auto max-w-2xl space-y-6">
      <div>
        <Button variant="ghost" size="sm" asChild className="mb-2 -ml-2">
          <Link href="/quotations">
            <ArrowLeft /> กลับไปหน้าใบเสนอราคา
          </Link>
        </Button>
        <h1 className="text-2xl font-semibold tracking-tight">สร้างใบเสนอราคา</h1>
        <p className="text-sm text-muted-foreground">ระบบจะคำนวณเบี้ยให้อัตโนมัติตามชั้นความคุ้มครอง</p>
      </div>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">ข้อมูลใบเสนอราคา</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="space-y-2">
            <Label required>ลูกค้า</Label>
            <Select value={form.customerId} onValueChange={(v) => setForm({ ...form, customerId: v, vehicleId: '' })}>
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

          <div className="grid grid-cols-2 gap-4">
            <div className="space-y-2">
              <Label>ส่วนลดประวัติดี (NCB)</Label>
              <Select value={form.ncbPercent} onValueChange={(v) => setForm({ ...form, ncbPercent: v })}>
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {NCB_STEPS.map((n) => (
                    <SelectItem key={n} value={String(n)}>
                      {n}%
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2">
              <Label>ค่าเสียหายส่วนแรก (Deductible)</Label>
              <Select value={form.deductible} onValueChange={(v) => setForm({ ...form, deductible: v })}>
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {DEDUCTIBLES.map((d) => (
                    <SelectItem key={d} value={String(d)}>
                      {d === 0 ? 'ไม่มี' : fmtBaht(d)}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
          </div>

          {/* Add-on riders (ความคุ้มครองเสริม) — toggle on/off; premiums add to the net. */}
          {!!riders?.length && (
            <div className="space-y-2">
              <Label>ความคุ้มครองเสริม</Label>
              <div className="grid gap-2 sm:grid-cols-2">
                {riders.map((rd) => {
                  const on = riderIds.includes(rd.id);
                  return (
                    <button
                      key={rd.id}
                      type="button"
                      onClick={() => toggleRider(rd.id)}
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

          {/* Live premium breakdown */}
          <div className="rounded-md border bg-muted/30 p-3">
            <div className="mb-2 flex items-center justify-between">
              <Label className="text-sm">สรุปเบี้ยประกัน</Label>
              {previewing && <span className="text-xs text-muted-foreground">กำลังคำนวณ…</span>}
            </div>
            {!canPreview ? (
              <p className="text-sm text-muted-foreground">เลือกรถ ชั้นความคุ้มครอง และทุนประกัน เพื่อคำนวณเบี้ย</p>
            ) : breakdown ? (
              <dl className="space-y-1 text-sm">
                <Row label="เบี้ยฐาน" value={fmtBaht(breakdown.basePremium)} />
                {breakdown.vehicleAgeLoading > 0 && (
                  <Row label="โหลดตามอายุรถ" value={`+${fmtBaht(breakdown.vehicleAgeLoading)}`} />
                )}
                {breakdown.ncbDiscount > 0 && (
                  <Row label={`ส่วนลด NCB ${form.ncbPercent}%`} value={`−${fmtBaht(breakdown.ncbDiscount)}`} negative />
                )}
                {breakdown.deductibleDiscount > 0 && (
                  <Row label="ส่วนลดค่าเสียหายส่วนแรก" value={`−${fmtBaht(breakdown.deductibleDiscount)}`} negative />
                )}
                {breakdown.ridersTotal > 0 && (
                  <Row label="ความคุ้มครองเสริม" value={`+${fmtBaht(breakdown.ridersTotal)}`} />
                )}
                <div className="mt-2 flex items-center justify-between border-t pt-2 text-base font-semibold">
                  <span>เบี้ยสุทธิ</span>
                  <span className="tabular-nums text-primary">{fmtBaht(breakdown.netPremium)}</span>
                </div>
              </dl>
            ) : (
              <p className="text-sm text-muted-foreground">—</p>
            )}
          </div>

          {/* Named drivers (กฎใหม่: ระบุผู้ขับขี่ 1–5 คน พร้อมรูปบัตรประชาชน) */}
          <div className="space-y-3 rounded-md border p-3">
            <div className="flex items-center justify-between">
              <Label required>ผู้ขับขี่ระบุชื่อ (1–{MAX_DRIVERS} คน)</Label>
              <Button
                type="button"
                size="sm"
                variant="outline"
                disabled={drivers.length >= MAX_DRIVERS}
                onClick={() => setDrivers((ds) => [...ds, emptyDriver()])}
              >
                <Plus /> เพิ่มผู้ขับขี่
              </Button>
            </div>
            {drivers.map((d, i) => (
              <div key={i} className="space-y-2 rounded border bg-muted/30 p-3">
                <div className="flex items-center justify-between">
                  <span className="text-sm font-medium">คนที่ {i + 1}</span>
                  {drivers.length > 1 && (
                    <Button
                      type="button"
                      size="sm"
                      variant="ghost"
                      onClick={() => setDrivers((ds) => ds.filter((_, idx) => idx !== i))}
                    >
                      <Trash2 className="text-destructive" />
                    </Button>
                  )}
                </div>
                <div className="grid grid-cols-2 gap-3">
                  <div className="space-y-1">
                    <Label>ชื่อ-นามสกุล</Label>
                    <Input
                      value={d.fullName}
                      onChange={(e) => setDriver(i, { fullName: e.target.value })}
                      placeholder="สมชาย ใจดี"
                    />
                  </div>
                  <div className="space-y-1">
                    <Label>เลขบัตรประชาชน (13 หลัก)</Label>
                    <Input
                      value={d.nationalId}
                      onChange={(e) => setDriver(i, { nationalId: e.target.value })}
                      maxLength={13}
                      placeholder="1100000000001"
                    />
                  </div>
                </div>
                <div className="space-y-1">
                  <Label>รูปบัตรประชาชน</Label>
                  <div className="flex items-center gap-3">
                    {d.idCardImagePath && (
                      <ImagePreview src={fileUrl(d.idCardImagePath)} alt={`บัตรประชาชน คนที่ ${i + 1}`} />
                    )}
                    <div className="flex flex-col items-start gap-1">
                      <Button
                        type="button"
                        size="sm"
                        variant="outline"
                        disabled={d.uploading}
                        onClick={() => document.getElementById(`driver-file-${i}`)?.click()}
                      >
                        {d.uploading ? (
                          'กำลังอัปโหลด…'
                        ) : (
                          <>
                            <Upload /> {d.idCardImagePath ? 'เปลี่ยนรูป' : 'เลือกรูป'}
                          </>
                        )}
                      </Button>
                      {d.idCardImagePath && (
                        <span className="inline-flex items-center gap-1 text-xs text-green-600">
                          <Check className="size-3.5" /> แนบรูปแล้ว
                        </span>
                      )}
                    </div>
                    <input
                      id={`driver-file-${i}`}
                      type="file"
                      accept="image/jpeg,image/png"
                      className="hidden"
                      onChange={(e) => {
                        const file = e.target.files?.[0];
                        if (file) uploadDriverImage(i, file);
                        e.target.value = '';
                      }}
                    />
                  </div>
                </div>
              </div>
            ))}
          </div>

          <div className="flex justify-end gap-2 pt-2">
            <Button variant="outline" asChild>
              <Link href="/quotations">ยกเลิก</Link>
            </Button>
            <Button
              onClick={submit}
              disabled={
                creating ||
                !form.customerId ||
                !form.vehicleId ||
                !form.coverageType ||
                !form.sumInsured ||
                !driversValid
              }
            >
              {creating ? 'กำลังคำนวณ…' : 'สร้าง'}
            </Button>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
