'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import Link from 'next/link';
import { toast } from 'sonner';
import { ArrowLeft, Car } from 'lucide-react';
import {
  useCreateVehicleMutation,
  useGetCustomersQuery,
  useGetVehicleBrandsQuery,
  useGetVehicleModelsQuery,
  useGetVehicleSubmodelsQuery,
  useGetVehicleModelYearsQuery,
  POWERTRAIN_LABELS,
} from '@/lib/api/insuranceApi';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { ProvinceSelect } from '@/components/address-select';
import { PageHeader } from '@/components/page-header';
import { apiError } from '@/lib/utils';

const emptyForm = {
  customerId: '',
  registrationNo: '',
  province: '',
  brandId: '',
  modelId: '',
  submodelId: '',
  modelYearId: '',
  chassisNo: '',
};

export default function NewVehiclePage() {
  const router = useRouter();
  const [createVehicle, { isLoading: saving }] = useCreateVehicleMutation();
  const { data: customers } = useGetCustomersQuery({ pageSize: 100 });
  const [form, setForm] = useState(emptyForm);

  // Cascading lookups — each fires only when its parent is chosen.
  const { data: brands } = useGetVehicleBrandsQuery();
  const { data: models } = useGetVehicleModelsQuery(Number(form.brandId), { skip: !form.brandId });
  const { data: submodels } = useGetVehicleSubmodelsQuery(Number(form.modelId), { skip: !form.modelId });
  const { data: years } = useGetVehicleModelYearsQuery(Number(form.submodelId), { skip: !form.submodelId });

  const submit = async () => {
    try {
      await createVehicle({
        customerId: Number(form.customerId),
        registrationNo: form.registrationNo,
        province: form.province,
        modelYearId: Number(form.modelYearId),
        chassisNo: form.chassisNo || undefined,
      }).unwrap();
      toast.success('เพิ่มรถยนต์แล้ว');
      router.push('/vehicles');
    } catch (e) {
      toast.error(apiError(e));
    }
  };

  return (
    <div className="mx-auto max-w-2xl space-y-6">
      <div>
        <Button variant="ghost" size="sm" asChild className="mb-2 -ml-2">
          <Link href="/vehicles">
            <ArrowLeft /> กลับไปหน้ารถยนต์
          </Link>
        </Button>
        <PageHeader icon={Car} title="เพิ่มรถยนต์" description="เลือกยี่ห้อ → รุ่น → รุ่นย่อย → ปี ตามลำดับ" />
      </div>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">ข้อมูลรถยนต์</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="space-y-2">
            <Label required>ลูกค้าเจ้าของ</Label>
            <Select value={form.customerId} onValueChange={(v) => setForm({ ...form, customerId: v })}>
              <SelectTrigger>
                <SelectValue placeholder="เลือกลูกค้า" />
              </SelectTrigger>
              <SelectContent>
                {customers?.items.map((c) => (
                  <SelectItem key={c.id} value={String(c.id)}>
                    {c.fullName} ({c.nationalId})
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          <div className="grid grid-cols-2 gap-4">
            <div className="space-y-2">
              <Label htmlFor="reg" required>ทะเบียน</Label>
              <Input id="reg" value={form.registrationNo} onChange={(e) => setForm({ ...form, registrationNo: e.target.value })} placeholder="กก1234" />
            </div>
            <div className="space-y-2">
              <Label htmlFor="province" required>จังหวัด</Label>
              <ProvinceSelect
                id="province"
                value={form.province}
                onChange={(province) => setForm({ ...form, province })}
              />
            </div>
          </div>

          <div className="grid grid-cols-2 gap-4">
            <div className="space-y-2">
              <Label required>ยี่ห้อ</Label>
              <Select
                value={form.brandId}
                onValueChange={(v) => setForm({ ...form, brandId: v, modelId: '', submodelId: '', modelYearId: '' })}
              >
                <SelectTrigger>
                  <SelectValue placeholder="เลือกยี่ห้อ" />
                </SelectTrigger>
                <SelectContent>
                  {brands?.map((b) => (
                    <SelectItem key={b.id} value={String(b.id)}>
                      {b.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2">
              <Label required>รุ่น</Label>
              <Select
                value={form.modelId}
                onValueChange={(v) => setForm({ ...form, modelId: v, submodelId: '', modelYearId: '' })}
                disabled={!form.brandId}
              >
                <SelectTrigger>
                  <SelectValue placeholder={form.brandId ? 'เลือกรุ่น' : 'เลือกยี่ห้อก่อน'} />
                </SelectTrigger>
                <SelectContent>
                  {models?.map((m) => (
                    <SelectItem key={m.id} value={String(m.id)}>
                      {m.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
          </div>

          <div className="grid grid-cols-2 gap-4">
            <div className="space-y-2">
              <Label required>รุ่นย่อย</Label>
              <Select
                value={form.submodelId}
                onValueChange={(v) => setForm({ ...form, submodelId: v, modelYearId: '' })}
                disabled={!form.modelId}
              >
                <SelectTrigger>
                  <SelectValue placeholder={form.modelId ? 'เลือกรุ่นย่อย' : 'เลือกรุ่นก่อน'} />
                </SelectTrigger>
                <SelectContent>
                  {submodels?.map((s) => (
                    <SelectItem key={s.id} value={String(s.id)}>
                      {s.name} · {POWERTRAIN_LABELS[s.powertrain]}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2">
              <Label required>ปีรถ</Label>
              <Select
                value={form.modelYearId}
                onValueChange={(v) => setForm({ ...form, modelYearId: v })}
                disabled={!form.submodelId}
              >
                <SelectTrigger>
                  <SelectValue placeholder={form.submodelId ? 'เลือกปี' : 'เลือกรุ่นย่อยก่อน'} />
                </SelectTrigger>
                <SelectContent>
                  {years?.map((y) => (
                    <SelectItem key={y.id} value={String(y.id)}>
                      {y.year}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
          </div>

          <div className="space-y-2">
            <Label htmlFor="chassis">เลขตัวถัง (ไม่บังคับ)</Label>
            <Input id="chassis" value={form.chassisNo} onChange={(e) => setForm({ ...form, chassisNo: e.target.value })} />
          </div>

          <div className="flex justify-end gap-2 pt-2">
            <Button variant="outline" asChild>
              <Link href="/vehicles">ยกเลิก</Link>
            </Button>
            <Button
              onClick={submit}
              disabled={saving || !form.customerId || !form.registrationNo || !form.province || !form.modelYearId}
            >
              {saving ? 'กำลังบันทึก…' : 'บันทึก'}
            </Button>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
