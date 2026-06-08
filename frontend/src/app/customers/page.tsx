'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { toast } from 'sonner';
import { Plus, Pencil, Trash2 } from 'lucide-react';
import { useGetCustomersQuery, useDeleteCustomerMutation, type CustomerDto } from '@/lib/api/insuranceApi';
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
import { Can } from '@/components/can';
import { P } from '@/lib/auth/permissions';
import { apiError } from '@/lib/utils';
import { useDebouncedValue } from '@/lib/use-debounced';

const PAGE_SIZE = 10;

export default function CustomersPage() {
  const router = useRouter();
  const [page, setPage] = useState(1);
  const [searchInput, setSearchInput] = useState('');
  const search = useDebouncedValue(searchInput, 300);
  const { data, isFetching } = useGetCustomersQuery({ page, pageSize: PAGE_SIZE, search });
  const [deleteCustomer, { isLoading: deleting }] = useDeleteCustomerMutation();
  const [deleteTarget, setDeleteTarget] = useState<CustomerDto | null>(null);

  const submitDelete = async () => {
    if (!deleteTarget) return;
    try {
      await deleteCustomer(deleteTarget.id).unwrap();
      toast.success('ลบลูกค้าแล้ว');
      setDeleteTarget(null);
    } catch (e) {
      toast.error(apiError(e));
    }
  };

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">ลูกค้า</h1>
          <p className="text-sm text-muted-foreground">จัดการข้อมูลผู้เอาประกัน</p>
        </div>
        <Can permission={P.CustomerWrite}>
          <Button onClick={() => router.push('/customers/new')}>
            <Plus /> เพิ่มลูกค้า
          </Button>
        </Can>
      </div>

      <DataTable<CustomerDto>
        rows={data?.items}
        loading={isFetching}
        getKey={(c) => c.id}
        page={page}
        pageSize={PAGE_SIZE}
        totalCount={data?.totalCount ?? 0}
        onPageChange={setPage}
        search={searchInput}
        onSearchChange={(v) => {
          setSearchInput(v);
          setPage(1);
        }}
        searchPlaceholder="ค้นหาชื่อ / เลขบัตร / อีเมล"
        emptyText="ยังไม่มีลูกค้า"
        columns={[
          { header: 'เลขบัตรประชาชน', cell: (c) => <span className="font-medium tabular-nums">{c.nationalId}</span> },
          {
            header: 'ชื่อ-นามสกุล',
            cell: (c) => (
              <button
                type="button"
                className="font-medium text-primary hover:underline"
                onClick={() => router.push(`/customers/${c.id}`)}
              >
                {c.fullName}
              </button>
            ),
          },
          { header: 'โทรศัพท์', cell: (c) => c.phone ?? '-' },
          { header: 'อีเมล', cell: (c) => c.email ?? '-' },
          { header: 'จังหวัด', cell: (c) => c.provinceName ?? '-' },
          {
            header: 'จัดการ',
            className: 'text-right',
            cell: (c) => (
              <Can permission={P.CustomerWrite}>
                <div className="flex justify-end gap-1">
                  <Button size="sm" variant="ghost" onClick={() => router.push(`/customers/${c.id}/edit`)}>
                    <Pencil />
                  </Button>
                  <Button size="sm" variant="ghost" onClick={() => setDeleteTarget(c)}>
                    <Trash2 className="text-destructive" />
                  </Button>
                </div>
              </Can>
            ),
          },
        ]}
      />

      {/* Delete confirm */}
      <Dialog open={!!deleteTarget} onOpenChange={(o) => !o && setDeleteTarget(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>ลบลูกค้า</DialogTitle>
            <DialogDescription>
              ต้องการลบ {deleteTarget?.fullName} ({deleteTarget?.nationalId}) หรือไม่? ลบได้เฉพาะลูกค้าที่ยังไม่มีรถ/ใบเสนอราคา/กรมธรรม์
            </DialogDescription>
          </DialogHeader>
          <DialogFooter>
            <Button variant="outline" onClick={() => setDeleteTarget(null)}>
              ยกเลิก
            </Button>
            <Button variant="destructive" onClick={submitDelete} disabled={deleting}>
              {deleting ? 'กำลังลบ…' : 'ลบ'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
