'use client';

import { ShieldPlus } from 'lucide-react';
import {
  useGetRidersQuery,
  useCreateRiderMutation,
  useUpdateRiderMutation,
  useDeleteRiderMutation,
  type Rider,
} from '@/lib/api/insuranceApi';
import { MasterTable } from '@/components/master-table';
import { P } from '@/lib/auth/permissions';
import { fmtBaht } from '@/lib/utils';

export default function RidersPage() {
  const { data, isFetching } = useGetRidersQuery();
  const [create] = useCreateRiderMutation();
  const [update] = useUpdateRiderMutation();
  const [remove] = useDeleteRiderMutation();

  return (
    <MasterTable<Rider>
      title="ความคุ้มครองเสริม"
      description="ความคุ้มครองเสริม (rider) ที่เลือกแนบกับกรมธรรม์ พร้อมเบี้ยประกัน"
      icon={ShieldPlus}
      rows={data}
      loading={isFetching}
      getId={(r) => r.id}
      getName={(r) => r.name}
      permission={P.LookupManage}
      addLabel="เพิ่มความคุ้มครอง"
      searchPlaceholder="ค้นหาความคุ้มครอง"
      emptyText="ยังไม่มีความคุ้มครองเสริม"
      columns={[
        { header: 'ชื่อความคุ้มครอง', cell: (r) => <span className="font-medium">{r.name}</span> },
        { header: 'เบี้ย', className: 'text-right tabular-nums', cell: (r) => fmtBaht(r.premium) },
      ]}
      fields={[
        { name: 'name', label: 'ชื่อความคุ้มครอง', placeholder: 'คุ้มครองน้ำท่วม' },
        { name: 'premium', label: 'เบี้ย (บาท)', type: 'number', placeholder: '1000' },
      ]}
      toValues={(r) => ({ name: r.name, premium: String(r.premium) })}
      onCreate={(v) => create({ name: v.name, premium: Number(v.premium) }).unwrap()}
      onUpdate={(id, v) => update({ id, name: v.name, premium: Number(v.premium) }).unwrap()}
      onDelete={(id) => remove(id).unwrap()}
    />
  );
}
