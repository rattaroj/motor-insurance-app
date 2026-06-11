'use client';

import { Suspense, useState } from 'react';
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
import { PageHeader } from '@/components/page-header';
import { StatusBadge } from '@/components/StatusBadge';
import { P } from '@/lib/auth/permissions';
import { useListUrlState } from '@/lib/use-url-state';
import { apiError, fmtDateTime } from '@/lib/utils';

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
      <span className="inline-block rounded-full bg-slate-500/10 px-2 py-0.5 text-xs font-medium text-slate-700 dark:text-slate-300">
        {channelLabel[n.channel] ?? n.channel}
      </span>
    ),
  },
  { header: 'ผู้รับ', cell: (n) => <span className="text-muted-foreground">{n.recipient}</span> },
  { header: 'หัวข้อ', cell: (n) => n.subject },
  {
    header: 'สถานะ',
    className: 'text-center',
    cell: (n) => <StatusBadge status={n.status} />,
  },
];

function NotificationsPageContent() {
  const { page, setPage, searchInput, onSearchChange, search } = useListUrlState();
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
      <PageHeader
        icon={Bell}
        title="ประวัติการแจ้งเตือน"
        description="บันทึกการแจ้งเตือนที่ส่งออก (เตือนต่ออายุ ฯลฯ) ทุกช่องทาง"
      />

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
        onSearchChange={onSearchChange}
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

export default function NotificationsPage() {
  return (
    <Suspense fallback={null}>
      <NotificationsPageContent />
    </Suspense>
  );
}
