'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { Plus } from 'lucide-react';
import { useGetVehiclesQuery, POWERTRAIN_LABELS, type VehicleDto } from '@/lib/api/insuranceApi';
import { Button } from '@/components/ui/button';
import { DataTable } from '@/components/data-table';
import { Can } from '@/components/can';
import { P } from '@/lib/auth/permissions';
import { useDebouncedValue } from '@/lib/use-debounced';

const PAGE_SIZE = 10;

export default function VehiclesPage() {
  const router = useRouter();
  const [page, setPage] = useState(1);
  const [searchInput, setSearchInput] = useState('');
  const search = useDebouncedValue(searchInput, 300);
  const { data, isFetching } = useGetVehiclesQuery({ page, pageSize: PAGE_SIZE, search });

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">รถยนต์</h1>
          <p className="text-sm text-muted-foreground">ทะเบียนรถที่เอาประกัน</p>
        </div>
        <Can permission={P.VehicleWrite}>
          <Button onClick={() => router.push('/vehicles/new')}>
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
          { header: 'พลังงาน', cell: (v) => POWERTRAIN_LABELS[v.powertrain] },
          { header: 'ปี', cell: (v) => <span className="tabular-nums">{v.year}</span> },
          { header: 'เจ้าของ', cell: (v) => v.customerName },
        ]}
      />
    </div>
  );
}
