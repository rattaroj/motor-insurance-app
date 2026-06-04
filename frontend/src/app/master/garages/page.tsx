'use client';

import { Wrench } from 'lucide-react';
import {
  useGetGaragesQuery,
  useCreateGarageMutation,
  useUpdateGarageMutation,
  useDeleteGarageMutation,
  type Garage,
} from '@/lib/api/insuranceApi';
import { MasterTable } from '@/components/master-table';
import { P } from '@/lib/auth/permissions';

export default function GaragesPage() {
  const { data, isFetching } = useGetGaragesQuery();
  const [create] = useCreateGarageMutation();
  const [update] = useUpdateGarageMutation();
  const [remove] = useDeleteGarageMutation();

  return (
    <MasterTable<Garage>
      title="อู่/ศูนย์ซ่อม"
      description="อู่และศูนย์ซ่อมที่ใช้อ้างอิงตอนรับเคลม"
      icon={Wrench}
      rows={data}
      loading={isFetching}
      getId={(g) => g.id}
      getName={(g) => g.name}
      searchText={(g) => `${g.name} ${g.phone ?? ''}`}
      permission={P.LookupManage}
      addLabel="เพิ่มอู่/ศูนย์ซ่อม"
      searchPlaceholder="ค้นหาชื่อ / เบอร์โทร"
      emptyText="ยังไม่มีอู่/ศูนย์ซ่อม"
      columns={[
        { header: 'ชื่ออู่/ศูนย์ซ่อม', cell: (g) => <span className="font-medium">{g.name}</span> },
        { header: 'โทรศัพท์', cell: (g) => g.phone ?? '-' },
      ]}
      fields={[
        { name: 'name', label: 'ชื่ออู่/ศูนย์ซ่อม', placeholder: 'อู่สมชายการช่าง' },
        { name: 'phone', label: 'โทรศัพท์', required: false, placeholder: '02-xxx-xxxx' },
      ]}
      toValues={(g) => ({ name: g.name, phone: g.phone ?? '' })}
      onCreate={(v) => create({ name: v.name, phone: v.phone || undefined }).unwrap()}
      onUpdate={(id, v) => update({ id, name: v.name, phone: v.phone || undefined }).unwrap()}
      onDelete={(id) => remove(id).unwrap()}
    />
  );
}
