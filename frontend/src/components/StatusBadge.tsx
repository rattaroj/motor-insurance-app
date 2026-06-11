import { cn } from '@/lib/utils';

const styles: Record<string, { pill: string; dot: string }> = {
  // policy
  Draft: { pill: 'bg-slate-500/10 text-slate-700 dark:text-slate-400 ring-slate-500/20', dot: 'bg-slate-400' },
  Quoted: { pill: 'bg-sky-500/10 text-sky-700 dark:text-sky-400 ring-sky-600/20', dot: 'bg-sky-500' },
  Issued: { pill: 'bg-blue-500/10 text-blue-700 dark:text-blue-400 ring-blue-600/20', dot: 'bg-blue-500' },
  Active: { pill: 'bg-emerald-500/10 text-emerald-700 dark:text-emerald-400 ring-emerald-600/20', dot: 'bg-emerald-500' },
  Cancelled: { pill: 'bg-red-500/10 text-red-700 dark:text-red-400 ring-red-600/20', dot: 'bg-red-500' },
  Expired: { pill: 'bg-amber-500/10 text-amber-700 dark:text-amber-400 ring-amber-600/20', dot: 'bg-amber-500' },
  Suspended: { pill: 'bg-orange-500/10 text-orange-700 dark:text-orange-400 ring-orange-600/20', dot: 'bg-orange-500' },
  // claim
  Filed: { pill: 'bg-sky-500/10 text-sky-700 dark:text-sky-400 ring-sky-600/20', dot: 'bg-sky-500' },
  UnderReview: { pill: 'bg-indigo-500/10 text-indigo-700 dark:text-indigo-400 ring-indigo-600/20', dot: 'bg-indigo-500' },
  Assessment: { pill: 'bg-violet-500/10 text-violet-700 dark:text-violet-400 ring-violet-600/20', dot: 'bg-violet-500' },
  Approved: { pill: 'bg-emerald-500/10 text-emerald-700 dark:text-emerald-400 ring-emerald-600/20', dot: 'bg-emerald-500' },
  Rejected: { pill: 'bg-red-500/10 text-red-700 dark:text-red-400 ring-red-600/20', dot: 'bg-red-500' },
  Paid: { pill: 'bg-emerald-500/10 text-emerald-700 dark:text-emerald-400 ring-emerald-600/20', dot: 'bg-emerald-500' },
  Closed: { pill: 'bg-slate-500/10 text-slate-700 dark:text-slate-400 ring-slate-500/20', dot: 'bg-slate-400' },
  // payment
  Pending: { pill: 'bg-amber-500/10 text-amber-700 dark:text-amber-400 ring-amber-600/20', dot: 'bg-amber-500' },
  Failed: { pill: 'bg-red-500/10 text-red-700 dark:text-red-400 ring-red-600/20', dot: 'bg-red-500' },
  Refunded: { pill: 'bg-slate-500/10 text-slate-700 dark:text-slate-400 ring-slate-500/20', dot: 'bg-slate-400' },
  // notification
  Sent: { pill: 'bg-emerald-500/10 text-emerald-700 dark:text-emerald-400 ring-emerald-600/20', dot: 'bg-emerald-500' },
  // user account
  Enabled: { pill: 'bg-emerald-500/10 text-emerald-700 dark:text-emerald-400 ring-emerald-600/20', dot: 'bg-emerald-500' },
  Disabled: { pill: 'bg-slate-500/10 text-slate-500 dark:text-slate-400 ring-slate-500/20', dot: 'bg-slate-400' },
};

const fallback = { pill: 'bg-slate-500/10 text-slate-700 dark:text-slate-400 ring-slate-500/20', dot: 'bg-slate-400' };

const labels: Record<string, string> = {
  Draft: 'ฉบับร่าง',
  Quoted: 'เสนอราคา',
  Issued: 'ออกกรมธรรม์',
  Active: 'คุ้มครองอยู่',
  Cancelled: 'ยกเลิก',
  Expired: 'หมดอายุ',
  Suspended: 'ระงับชั่วคราว',
  Filed: 'แจ้งเคลม',
  UnderReview: 'กำลังตรวจสอบ',
  Assessment: 'ประเมินความเสียหาย',
  Approved: 'อนุมัติ',
  Rejected: 'ปฏิเสธ',
  Paid: 'ชำระแล้ว',
  Closed: 'ปิด',
  Pending: 'รอชำระ',
  Failed: 'ไม่สำเร็จ',
  Refunded: 'คืนเงินแล้ว',
  Sent: 'ส่งแล้ว',
  Enabled: 'ใช้งาน',
  Disabled: 'ปิดใช้งาน',
};

export function StatusBadge({ status }: { status: string }) {
  const s = styles[status] ?? fallback;
  return (
    <span
      className={cn(
        'inline-flex items-center gap-1.5 whitespace-nowrap rounded-full px-2.5 py-0.5 text-xs font-medium ring-1 ring-inset',
        s.pill,
      )}
    >
      <span aria-hidden className={cn('h-1.5 w-1.5 shrink-0 rounded-full', s.dot)} />
      {labels[status] ?? status}
    </span>
  );
}
