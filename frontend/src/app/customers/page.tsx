'use client';

import { useState } from 'react';
import { toast } from 'sonner';
import { Plus } from 'lucide-react';
import { useGetCustomersQuery, useCreateCustomerMutation, type CustomerDto } from '@/lib/api/insuranceApi';
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
import { Can } from '@/components/can';
import { P } from '@/lib/auth/permissions';
import { apiError } from '@/lib/utils';
import { useDebouncedValue } from '@/lib/use-debounced';

const empty = { nationalId: '', fullName: '', phone: '', email: '' };
const PAGE_SIZE = 10;

export default function CustomersPage() {
  const [page, setPage] = useState(1);
  const [searchInput, setSearchInput] = useState('');
  const search = useDebouncedValue(searchInput, 300);
  const { data, isFetching } = useGetCustomersQuery({ page, pageSize: PAGE_SIZE, search });
  const [createCustomer, { isLoading: saving }] = useCreateCustomerMutation();
  const [open, setOpen] = useState(false);
  const [form, setForm] = useState(empty);

  const submit = async () => {
    try {
      await createCustomer({
        nationalId: form.nationalId,
        fullName: form.fullName,
        phone: form.phone || undefined,
        email: form.email || undefined,
      }).unwrap();
      toast.success('เพิ่มลูกค้าแล้ว');
      setOpen(false);
      setForm(empty);
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
          <Button onClick={() => setOpen(true)}>
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
          { header: 'ชื่อ-นามสกุล', cell: (c) => c.fullName },
          { header: 'โทรศัพท์', cell: (c) => c.phone ?? '-' },
          { header: 'อีเมล', cell: (c) => c.email ?? '-' },
        ]}
      />

      <Dialog open={open} onOpenChange={setOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>เพิ่มลูกค้า</DialogTitle>
            <DialogDescription>กรอกข้อมูลผู้เอาประกันรายใหม่</DialogDescription>
          </DialogHeader>
          <div className="space-y-4">
            <div className="space-y-2">
              <Label htmlFor="nationalId" required>เลขบัตรประชาชน (13 หลัก)</Label>
              <Input
                id="nationalId"
                value={form.nationalId}
                onChange={(e) => setForm({ ...form, nationalId: e.target.value })}
                maxLength={13}
                placeholder="1100000000001"
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="fullName" required>ชื่อ-นามสกุล</Label>
              <Input
                id="fullName"
                value={form.fullName}
                onChange={(e) => setForm({ ...form, fullName: e.target.value })}
                placeholder="สมชาย ใจดี"
              />
            </div>
            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-2">
                <Label htmlFor="phone">โทรศัพท์</Label>
                <Input id="phone" value={form.phone} onChange={(e) => setForm({ ...form, phone: e.target.value })} />
              </div>
              <div className="space-y-2">
                <Label htmlFor="email">อีเมล</Label>
                <Input id="email" type="email" value={form.email} onChange={(e) => setForm({ ...form, email: e.target.value })} />
              </div>
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setOpen(false)}>
              ยกเลิก
            </Button>
            <Button onClick={submit} disabled={saving || !form.nationalId || !form.fullName}>
              {saving ? 'กำลังบันทึก…' : 'บันทึก'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
