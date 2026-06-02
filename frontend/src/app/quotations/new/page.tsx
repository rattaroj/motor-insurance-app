'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import Link from 'next/link';
import { toast } from 'sonner';
import { ArrowLeft, Plus, Trash2, Upload, Check } from 'lucide-react';
import {
  useCreateQuotationMutation,
  useGetCustomersQuery,
  useGetVehiclesQuery,
  useUploadIdCardMutation,
  fileUrl,
  type CoverageType,
  type DriverInput,
} from '@/lib/api/insuranceApi';
import { ImagePreview } from '@/components/image-preview';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { apiError } from '@/lib/utils';

const COVERAGES: { value: CoverageType; label: string }[] = [
  { value: 'Type1', label: 'ชั้น 1' },
  { value: 'Type2Plus', label: 'ชั้น 2+' },
  { value: 'Type3Plus', label: 'ชั้น 3+' },
  { value: 'Type3', label: 'ชั้น 3' },
];

const emptyCreate = { customerId: '', vehicleId: '', coverageType: '' as CoverageType | '', sumInsured: '' };
const MAX_DRIVERS = 5;

/** A driver row in the create form: the API payload plus UI-only upload state. */
type DriverRow = DriverInput & { fileName: string; uploading: boolean };
const emptyDriver = (): DriverRow => ({ fullName: '', nationalId: '', idCardImagePath: '', fileName: '', uploading: false });
const driverComplete = (d: DriverRow) =>
  d.fullName.trim() !== '' && d.nationalId.length === 13 && d.idCardImagePath !== '';

export default function NewQuotationPage() {
  const router = useRouter();
  const { data: customers } = useGetCustomersQuery({ pageSize: 100 });
  const [createQuotation, { isLoading: creating }] = useCreateQuotationMutation();
  const [uploadIdCard] = useUploadIdCardMutation();

  const [form, setForm] = useState(emptyCreate);
  const [drivers, setDrivers] = useState<DriverRow[]>([emptyDriver()]);

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
