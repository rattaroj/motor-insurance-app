'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useGetPoliciesQuery } from '@/lib/api/insuranceApi';
import { StatusBadge } from '@/components/StatusBadge';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from '@/components/ui/table';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { fmtBaht } from '@/lib/utils';

const STATUSES = ['Draft', 'Quoted', 'Issued', 'Active', 'Cancelled', 'Expired'];

export default function PoliciesPage() {
  const [page, setPage] = useState(1);
  const [status, setStatus] = useState('all');
  const { data, isLoading, isError, refetch } = useGetPoliciesQuery({
    page,
    pageSize: 10,
    status: status === 'all' ? undefined : status,
  });

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">กรมธรรม์</h1>
          <p className="text-sm text-muted-foreground">รายการกรมธรรม์ทั้งหมด</p>
        </div>
        <Select
          value={status}
          onValueChange={(v) => {
            setStatus(v);
            setPage(1);
          }}
        >
          <SelectTrigger className="w-44">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">ทุกสถานะ</SelectItem>
            {STATUSES.map((s) => (
              <SelectItem key={s} value={s}>
                {s}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>

      {isError && (
        <Card className="border-destructive/40 bg-destructive/5 p-4 text-sm text-destructive">
          โหลดข้อมูลไม่สำเร็จ — ตรวจว่า backend รันอยู่
          <button onClick={() => refetch()} className="ml-2 underline">
            ลองใหม่
          </button>
        </Card>
      )}

      <Card>
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>เลขกรมธรรม์</TableHead>
              <TableHead>ลูกค้า</TableHead>
              <TableHead>ทะเบียน</TableHead>
              <TableHead>สถานะ</TableHead>
              <TableHead className="text-right">เบี้ย</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {isLoading && (
              <TableRow>
                <TableCell colSpan={5} className="text-center text-muted-foreground">
                  กำลังโหลด…
                </TableCell>
              </TableRow>
            )}
            {data?.items.map((p) => (
              <TableRow key={p.id}>
                <TableCell>
                  <Link href={`/policies/${p.id}`} className="font-medium text-primary hover:underline">
                    {p.policyNo}
                  </Link>
                </TableCell>
                <TableCell>{p.customerName}</TableCell>
                <TableCell>{p.vehicleRegistration}</TableCell>
                <TableCell>
                  <StatusBadge status={p.status} />
                </TableCell>
                <TableCell className="text-right tabular-nums">{fmtBaht(p.premium)}</TableCell>
              </TableRow>
            ))}
            {data?.items.length === 0 && (
              <TableRow>
                <TableCell colSpan={5} className="text-center text-muted-foreground">
                  ไม่มีข้อมูล
                </TableCell>
              </TableRow>
            )}
          </TableBody>
        </Table>
      </Card>

      {data && (
        <div className="flex items-center justify-between text-sm text-muted-foreground">
          <span>
            ทั้งหมด {data.totalCount} รายการ — หน้า {data.page}/{data.totalPages || 1}
          </span>
          <div className="flex gap-2">
            <Button variant="outline" size="sm" disabled={page <= 1} onClick={() => setPage((p) => p - 1)}>
              ก่อนหน้า
            </Button>
            <Button
              variant="outline"
              size="sm"
              disabled={page >= (data.totalPages || 1)}
              onClick={() => setPage((p) => p + 1)}
            >
              ถัดไป
            </Button>
          </div>
        </div>
      )}
    </div>
  );
}
