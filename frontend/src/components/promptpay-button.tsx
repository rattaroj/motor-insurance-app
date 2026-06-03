'use client';

import { useState } from 'react';
import { toast } from 'sonner';
import { QrCode } from 'lucide-react';
import { useGetPromptPayQrMutation } from '@/lib/api/insuranceApi';
import { Button } from '@/components/ui/button';
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription } from '@/components/ui/dialog';
import { Skeleton } from '@/components/ui/skeleton';
import { apiError, fmtBaht } from '@/lib/utils';

/** Shows a PromptPay QR (scan-to-pay) for a pending inbound premium payment. */
export function PromptPayButton({ paymentId, paymentNo, amount }: { paymentId: number; paymentNo: string; amount: number }) {
  const [open, setOpen] = useState(false);
  const [url, setUrl] = useState<string | null>(null);
  const [getQr, { isLoading }] = useGetPromptPayQrMutation();

  const cleanup = () => {
    if (url) URL.revokeObjectURL(url);
    setUrl(null);
  };

  const load = async () => {
    setOpen(true);
    try {
      const blob = await getQr(paymentId).unwrap();
      setUrl(URL.createObjectURL(blob));
    } catch (e) {
      toast.error(apiError(e));
      setOpen(false);
    }
  };

  return (
    <>
      <Button size="sm" variant="ghost" onClick={load}>
        <QrCode /> QR พร้อมเพย์
      </Button>
      <Dialog
        open={open}
        onOpenChange={(o) => {
          if (!o) cleanup();
          setOpen(o);
        }}
      >
        <DialogContent className="sm:max-w-xs">
          <DialogHeader>
            <DialogTitle>สแกนจ่ายด้วยพร้อมเพย์</DialogTitle>
            <DialogDescription>
              {paymentNo} · {fmtBaht(amount)}
            </DialogDescription>
          </DialogHeader>
          <div className="flex justify-center py-2">
            {isLoading || !url ? (
              <Skeleton className="h-64 w-64" />
            ) : (
              // eslint-disable-next-line @next/next/no-img-element
              <img src={url} alt="PromptPay QR" className="h-64 w-64" />
            )}
          </div>
        </DialogContent>
      </Dialog>
    </>
  );
}
