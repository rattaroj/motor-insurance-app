'use client';

import { Suspense } from 'react';
import { useRouter } from 'next/navigation';
import { Plus, Car } from 'lucide-react';
import { useGetVehiclesQuery, POWERTRAIN_LABELS, type VehicleDto } from '@/lib/api/insuranceApi';
import { Button } from '@/components/ui/button';
import { DataTable } from '@/components/data-table';
import { Can } from '@/components/can';
import { PageHeader } from '@/components/page-header';
import { P } from '@/lib/auth/permissions';
import { useListUrlState } from '@/lib/use-url-state';

const PAGE_SIZE = 10;

function VehiclesPageContent() {
  const router = useRouter();
  const { page, setPage, searchInput, onSearchChange, search } = useListUrlState();
  const { data, isFetching } = useGetVehiclesQuery({ page, pageSize: PAGE_SIZE, search });

  return (
    <div className="space-y-6">
      <PageHeader
        icon={Car}
        title="รถยนต์"
        description="ทะเบียนรถที่เอาประกัน"
        actions={
          <Can permission={P.VehicleWrite}>
            <Button onClick={() => router.push('/vehicles/new')}>
              <Plus /> เพิ่มรถยนต์
            </Button>
          </Can>
        }
      />

      <DataTable<VehicleDto>
        rows={data?.items}
        loading={isFetching}
        getKey={(v) => v.id}
        page={page}
        pageSize={PAGE_SIZE}
        totalCount={data?.totalCount ?? 0}
        onPageChange={setPage}
        search={searchInput}
        onSearchChange={onSearchChange}
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

export default function VehiclesPage() {
  return (
    <Suspense fallback={null}>
      <VehiclesPageContent />
    </Suspense>
  );
}
