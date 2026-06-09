'use client';

import { useState } from 'react';
import { toast } from 'sonner';
import { Bell, RotateCw } from 'lucide-react';
import { useGetNotificationsQuery, useResendNotificationMutation } from '@/lib/api/insuranceApi';
import { DataTable, type Column } from '@/components/data-table';
import type { NotificationDto } from '@/lib/api/insuranceApi';
import { Button } from '@/components/ui/button';
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
import { useDebouncedValue } from '@/lib/use-debounced';
import { apiError, cn, fmtDateTime } from '@/lib/utils';

const PAGE_SIZE = 20;

const channelLabel: Record<string, string> = {
  Email: 'อีเมล',
  Sms: 'SMS',
  Line: 'LINE',
  Log: 'บันทึก',
};

const baseColumns: Column<NotificationDto>[] = [
  { header: 'วันที่', cell: (n) => fmtDateTime(n.sentAt ?? n.createdAt) },
  { header: 'กรมธรรม์', cell: (n) => <span className="font-medium">{n.policyNo ?? '-'}</span> },
  {
    header: 'ช่องทาง',
    cell: (n) => (
      <span className="inline-block rounded-full bg-slate-100 px-2 py-0.5 text-xs font-medium text-slate-700">
        {channelLabel[n.channel] ?? n.channel}
      </span>
    ),
  },
  { header: 'ผู้รับ', cell: (n) => <span className="text-muted-foreground">{n.recipient}</span> },
  { header: 'หัวข้อ', cell: (n) => n.subject },
  {
    header: 'สถานะ',
    className: 'text-center',
    cell: (n) => (
      <span
        className={cn(
          'inline-block rounded-full px-2 py-0.5 text-xs font-medium',
          n.status === 'Sent' ? 'bg-emerald-100 text-emerald-700' : 'bg-red-100 text-red-700',
        )}
      >
        {n.status === 'Sent' ? 'ส่งแล้ว' : 'ล้มเหลว'}
      </span>
    ),
  },
];

export default function NotificationsPage() {
  const [page, setPage] = useState(1);
  const [searchInput, setSearchInput] = useState('');
  const search = useDebouncedValue(searchInput, 300);
  const { data, isFetching } = useGetNotificationsQuery({ page, pageSize: PAGE_SIZE, search });
  const [resend, { isLoading: resending }] = useResendNotificationMutation();
  // Notification awaiting resend confirmation (null = dialog closed).
  const [resendFor, setResendFor] = useState<NotificationDto | null>(null);

  const doResend = async (id: number) => {
    try {
      const res = await resend(id).unwrap();
      toast.success(res.status === 'Sent' ? 'ส่งซ้ำสำเร็จ' : 'ส่งซ้ำแล้วแต่ยังล้มเหลว');
    } catch (e) {
      toast.error(apiError(e));
    }
  };

  const confirmResend = async () => {
    if (!resendFor) return;
    await doResend(resendFor.id);
    setResendFor(null);
  };

  const columns: Column<NotificationDto>[] = [
    ...baseColumns,
    {
      header: '',
      className: 'text-right w-[1%] whitespace-nowrap',
      cell: (n) =>
        n.status === 'Failed' ? (
          <Can permission={P.PolicyRenew}>
            <Button size="sm" variant="ghost" disabled={resending} onClick={() => setResendFor(n)}>
              <RotateCw /> ส่งซ้ำ
            </Button>
          </Can>
        ) : null,
    },
  ];

  return (
    <div className="space-y-6">
      <div className="flex items-center gap-3">
        <span className="flex h-10 w-10 shrink-0 items-center justify-center rounded-lg bg-primary/10 text-primary">
          <Bell className="h-5 w-5" />
        </span>
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">ประวัติการแจ้งเตือน</h1>
          <p className="text-sm text-muted-foreground">บันทึกการแจ้งเตือนที่ส่งออก (เตือนต่ออายุ ฯลฯ) ทุกช่องทาง</p>
        </div>
      </div>

      <DataTable<NotificationDto>
        rows={data?.items}
        loading={isFetching}
        getKey={(n) => n.id}
        columns={columns}
        page={page}
        pageSize={PAGE_SIZE}
        totalCount={data?.totalCount ?? 0}
        onPageChange={setPage}
        search={searchInput}
        onSearchChange={(v) => {
          setSearchInput(v);
          setPage(1);
        }}
        searchPlaceholder="ค้นหากรมธรรม์ / ผู้รับ / หัวข้อ"
        emptyText="ยังไม่มีประวัติการแจ้งเตือน"
      />

      {/* Confirm before resending a failed notification. */}
      <Dialog open={!!resendFor} onOpenChange={(o) => !o && setResendFor(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>ยืนยันการส่งซ้ำ</DialogTitle>
            <DialogDescription>
              ส่งการแจ้งเตือน “{resendFor?.subject}” ซ้ำไปยัง {resendFor?.recipient} (
              {resendFor ? (channelLabel[resendFor.channel] ?? resendFor.channel) : ''})
            </DialogDescription>
          </DialogHeader>
          <DialogFooter>
            <Button variant="outline" onClick={() => setResendFor(null)}>
              ยกเลิก
            </Button>
            <Button onClick={confirmResend} disabled={resending}>
              <RotateCw /> ยืนยันส่งซ้ำ
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
