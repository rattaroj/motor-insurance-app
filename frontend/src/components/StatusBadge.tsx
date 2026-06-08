import { cn } from '@/lib/utils';

const styles: Record<string, string> = {
  // policy
  Draft: 'bg-slate-100 text-slate-700',
  Quoted: 'bg-sky-100 text-sky-800',
  Issued: 'bg-blue-100 text-blue-800',
  Active: 'bg-emerald-100 text-emerald-800',
  Cancelled: 'bg-red-100 text-red-800',
  Expired: 'bg-amber-100 text-amber-800',
  Suspended: 'bg-orange-100 text-orange-800',
  // claim
  Filed: 'bg-sky-100 text-sky-800',
  UnderReview: 'bg-indigo-100 text-indigo-800',
  Assessment: 'bg-violet-100 text-violet-800',
  Approved: 'bg-emerald-100 text-emerald-800',
  Rejected: 'bg-red-100 text-red-800',
  Paid: 'bg-emerald-100 text-emerald-800',
  Closed: 'bg-slate-100 text-slate-700',
  // payment
  Pending: 'bg-amber-100 text-amber-800',
  Failed: 'bg-red-100 text-red-800',
  Refunded: 'bg-slate-100 text-slate-700',
};

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
};

export function StatusBadge({ status }: { status: string }) {
  return (
    <span
      className={cn(
        'inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-medium',
        styles[status] ?? 'bg-slate-100 text-slate-700',
      )}
    >
      {labels[status] ?? status}
    </span>
  );
}
