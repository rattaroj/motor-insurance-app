'use client';

import { useState } from 'react';
import { toast } from 'sonner';
import { Plus } from 'lucide-react';
import {
  useGetVehiclesQuery,
  useCreateVehicleMutation,
  useGetCustomersQuery,
  useGetVehicleBrandsQuery,
  useGetVehicleModelsQuery,
  useGetVehicleSubmodelsQuery,
  useGetVehicleModelYearsQuery,
  type VehicleDto,
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
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Can } from '@/components/can';
import { P } from '@/lib/auth/permissions';
import { apiError } from '@/lib/utils';
import { useDebouncedValue } from '@/lib/use-debounced';

const PAGE_SIZE = 10;
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

export default function VehiclesPage() {
  const [page, setPage] = useState(1);
  const [searchInput, setSearchInput] = useState('');
  const search = useDebouncedValue(searchInput, 300);
  const { data, isFetching } = useGetVehiclesQuery({ page, pageSize: PAGE_SIZE, search });
  const { data: customers } = useGetCustomersQuery({ pageSize: 100 });
  const [createVehicle, { isLoading: saving }] = useCreateVehicleMutation();
  const [open, setOpen] = useState(false);
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
      setOpen(false);
      setForm(emptyForm);
    } catch (e) {
      toast.error(apiError(e));
    }
  };

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">รถยนต์</h1>
          <p className="text-sm text-muted-foreground">ทะเบียนรถที่เอาประกัน</p>
        </div>
        <Can permission={P.VehicleWrite}>
          <Button onClick={() => setOpen(true)}>
            <Plus /> เพิ่มรถยนต์
          </Button>
        </Can>
      </div>

      <DataTable<VehicleDto>
        rows={data?.items}
        loading={isFetching}
        getKey={(v) => v.id}
        page={page}
        pageSize={PAGE_SIZE}
        totalCount={data?.totalCount ?? 0}
        onPageChange={setPage}
        search={searchInput}
        onSearchChange={(v) => {
          setSearchInput(v);
          setPage(1);
        }}
        searchPlaceholder="ค้นหาทะเบียน / ยี่ห้อ / เจ้าของ"
        emptyText="ยังไม่มีรถยนต์"
        columns={[
          { header: 'ทะเบียน', cell: (v) => <span className="font-medium">{v.registrationNo}</span> },
          { header: 'จังหวัด', cell: (v) => v.province },
          { header: 'ยี่ห้อ', cell: (v) => v.brand },
          { header: 'รุ่น', cell: (v) => v.model },
          { header: 'รุ่นย่อย', cell: (v) => v.submodel },
          { header: 'ปี', cell: (v) => <span className="tabular-nums">{v.year}</span> },
          { header: 'เจ้าของ', cell: (v) => v.customerName },
        ]}
      />

      <Dialog open={open} onOpenChange={setOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>เพิ่มรถยนต์</DialogTitle>
            <DialogDescription>เลือกยี่ห้อ → รุ่น → รุ่นย่อย → ปี ตามลำดับ</DialogDescription>
          </DialogHeader>
          <div className="space-y-4">
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
                <Input id="province" value={form.province} onChange={(e) => setForm({ ...form, province: e.target.value })} placeholder="กรุงเทพมหานคร" />
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
                        {s.name}
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
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setOpen(false)}>
              ยกเลิก
            </Button>
            <Button
              onClick={submit}
              disabled={
                saving || !form.customerId || !form.registrationNo || !form.province || !form.modelYearId
              }
            >
              {saving ? 'กำลังบันทึก…' : 'บันทึก'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
