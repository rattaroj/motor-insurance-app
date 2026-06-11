'use client';

import { useEffect, useState } from 'react';
import { toast } from 'sonner';
import { MessageSquareText, Pencil, Eye } from 'lucide-react';
import {
  useGetNotificationTemplatesQuery,
  useUpdateNotificationTemplateMutation,
  type NotificationTemplateDto,
} from '@/lib/api/insuranceApi';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Skeleton } from '@/components/ui/skeleton';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogDescription,
  DialogFooter,
} from '@/components/ui/dialog';
import { PageHeader } from '@/components/page-header';
import { apiError } from '@/lib/utils';

/** Sample values used to render the live preview. */
const SAMPLE: Record<string, string> = {
  customerName: 'สมชาย ใจดี',
  policyNo: 'POL-2026-000001',
  expiryDate: '31/12/2569',
  estimatedPremium: '12,500.00 บาท (ราคาจริงยืนยันเมื่อออกกรมธรรม์)',
  installmentSeq: '2',
  amount: '5,000.00',
  dueDate: '01/06/2569',
};

const render = (text: string) => text.replace(/\{\{\s*(\w+)\s*\}\}/g, (m, k) => SAMPLE[k] ?? m);

export default function NotificationTemplatesPage() {
  const { data, isLoading } = useGetNotificationTemplatesQuery();
  const [update, { isLoading: saving }] = useUpdateNotificationTemplateMutation();
  const [editing, setEditing] = useState<NotificationTemplateDto | null>(null);
  const [subject, setSubject] = useState('');
  const [body, setBody] = useState('');

  useEffect(() => {
    if (editing) {
      setSubject(editing.subject);
      setBody(editing.body);
    }
  }, [editing]);

  const save = async () => {
    if (!editing) return;
    try {
      await update({ key: editing.key, subject, body }).unwrap();
      toast.success('บันทึกเทมเพลตแล้ว');
      setEditing(null);
    } catch (e) {
      toast.error(apiError(e));
    }
  };

  const insertVar = (v: string) => setBody((b) => `${b}{{${v}}}`);

  return (
    <div className="space-y-6">
      <PageHeader
        icon={MessageSquareText}
        title="เทมเพลตการแจ้งเตือน"
        description="ปรับข้อความหัวเรื่องและเนื้อหาของการแจ้งเตือนได้เอง โดยไม่ต้อง deploy — ใช้ตัวแปร {{...}} แทนค่าจริงตอนส่ง"
      />

      {isLoading ? (
        <div className="grid gap-4 lg:grid-cols-2">
          {Array.from({ length: 2 }).map((_, i) => (
            <Skeleton key={i} className="h-48" />
          ))}
        </div>
      ) : (
        <div className="grid gap-4 lg:grid-cols-2">
          {data?.map((t) => (
            <Card key={t.key}>
              <CardHeader className="flex-row items-center justify-between space-y-0">
                <CardTitle className="text-base">{t.label}</CardTitle>
                <Button size="sm" variant="outline" onClick={() => setEditing(t)}>
                  <Pencil /> แก้ไข
                </Button>
              </CardHeader>
              <CardContent className="space-y-3">
                <div>
                  <p className="text-xs text-muted-foreground">หัวเรื่อง</p>
                  <p className="font-medium">{t.subject}</p>
                </div>
                <div>
                  <p className="text-xs text-muted-foreground">เนื้อหา</p>
                  <p className="whitespace-pre-line text-sm">{t.body}</p>
                </div>
                <div className="flex flex-wrap gap-1.5 pt-1">
                  {t.variables.map((v) => (
                    <code key={v} className="rounded bg-muted px-1.5 py-0.5 text-xs text-muted-foreground">
                      {`{{${v}}}`}
                    </code>
                  ))}
                </div>
              </CardContent>
            </Card>
          ))}
        </div>
      )}

      <Dialog open={editing !== null} onOpenChange={(o) => !o && setEditing(null)}>
        <DialogContent className="max-w-2xl">
          <DialogHeader>
            <DialogTitle>แก้ไขเทมเพลต — {editing?.label}</DialogTitle>
            <DialogDescription>คลิกตัวแปรเพื่อแทรกลงในเนื้อหา ดูตัวอย่างผลลัพธ์ด้านล่าง</DialogDescription>
          </DialogHeader>

          <div className="space-y-4">
            <div className="space-y-2">
              <Label htmlFor="subject" required>หัวเรื่อง</Label>
              <Input id="subject" value={subject} onChange={(e) => setSubject(e.target.value)} />
            </div>
            <div className="space-y-2">
              <Label htmlFor="body" required>เนื้อหา</Label>
              <Textarea id="body" rows={6} value={body} onChange={(e) => setBody(e.target.value)} />
              <div className="flex flex-wrap items-center gap-1.5">
                <span className="text-xs text-muted-foreground">แทรกตัวแปร:</span>
                {editing?.variables.map((v) => (
                  <button
                    key={v}
                    type="button"
                    onClick={() => insertVar(v)}
                    className="rounded bg-muted px-1.5 py-0.5 text-xs text-muted-foreground transition-colors hover:bg-accent hover:text-accent-foreground"
                  >
                    {`{{${v}}}`}
                  </button>
                ))}
              </div>
            </div>

            <div className="rounded-lg border bg-muted/30 p-3">
              <p className="mb-1 flex items-center gap-1.5 text-xs font-medium text-muted-foreground">
                <Eye className="h-3.5 w-3.5" /> ตัวอย่าง (ใช้ข้อมูลตัวอย่าง)
              </p>
              <p className="text-sm font-medium">{render(subject)}</p>
              <p className="mt-1 whitespace-pre-line text-sm text-muted-foreground">{render(body)}</p>
            </div>
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setEditing(null)}>
              ยกเลิก
            </Button>
            <Button onClick={save} disabled={saving || !subject.trim() || !body.trim()}>
              {saving ? 'กำลังบันทึก…' : 'บันทึก'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
