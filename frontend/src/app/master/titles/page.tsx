'use client';

import { UserSquare2 } from 'lucide-react';
import {
  useGetCustomerTitlesQuery,
  useCreateCustomerTitleMutation,
  useUpdateCustomerTitleMutation,
  useDeleteCustomerTitleMutation,
  type Option,
} from '@/lib/api/insuranceApi';
import { MasterTable } from '@/components/master-table';
import { P } from '@/lib/auth/permissions';

export default function CustomerTitlesPage() {
  const { data, isFetching } = useGetCustomerTitlesQuery();
  const [create] = useCreateCustomerTitleMutation();
  const [update] = useUpdateCustomerTitleMutation();
  const [remove] = useDeleteCustomerTitleMutation();

  return (
    <MasterTable<Option>
      title="คำนำหน้าชื่อ"
      description="คำนำหน้าชื่อลูกค้า เช่น นาย นาง นางสาว"
      icon={UserSquare2}
      rows={data}
      loading={isFetching}
      getId={(t) => t.id}
      getName={(t) => t.name}
      permission={P.LookupManage}
      addLabel="เพิ่มคำนำหน้า"
      searchPlaceholder="ค้นหาคำนำหน้า"
      emptyText="ยังไม่มีคำนำหน้าชื่อ"
      columns={[{ header: 'คำนำหน้า', cell: (t) => <span className="font-medium">{t.name}</span> }]}
      fields={[{ name: 'name', label: 'คำนำหน้า', placeholder: 'นาย' }]}
      toValues={(t) => ({ name: t.name })}
      onCreate={(v) => create({ name: v.name }).unwrap()}
      onUpdate={(id, v) => update({ id, name: v.name }).unwrap()}
      onDelete={(id) => remove(id).unwrap()}
    />
  );
}
