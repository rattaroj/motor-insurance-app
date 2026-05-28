'use client';

import { useState } from 'react';
import { toast } from 'sonner';
import { Plus, Pencil, Trash2, ChevronRight } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardHeader, CardTitle, CardContent } from '@/components/ui/card';
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
import { cn, apiError } from '@/lib/utils';

export type MasterItem = { id: number; label: string };

interface Props {
  title: string;
  items: MasterItem[] | undefined;
  selectedId: number | null;
  onSelect: (id: number) => void;
  onAdd: (value: string) => Promise<unknown>;
  onEdit: (id: number, value: string) => Promise<unknown>;
  onDelete: (id: number) => Promise<unknown>;
  fieldLabel: string;
  inputType?: 'text' | 'number';
  disabled?: boolean;
  disabledHint?: string;
  selectable?: boolean;
}

export function MasterColumn({
  title,
  items,
  selectedId,
  onSelect,
  onAdd,
  onEdit,
  onDelete,
  fieldLabel,
  inputType = 'text',
  disabled = false,
  disabledHint = 'เลือกรายการทางซ้ายก่อน',
  selectable = true,
}: Props) {
  const [addOpen, setAddOpen] = useState(false);
  const [value, setValue] = useState('');
  const [editItem, setEditItem] = useState<MasterItem | null>(null);
  const [deleteItem, setDeleteItem] = useState<MasterItem | null>(null);
  const [busy, setBusy] = useState(false);

  const run = async (fn: () => Promise<unknown>, ok: string, done: () => void) => {
    setBusy(true);
    try {
      await fn();
      toast.success(ok);
      done();
    } catch (e) {
      toast.error(apiError(e));
    } finally {
      setBusy(false);
    }
  };

  return (
    <Card className={cn('flex flex-col', disabled && 'opacity-60')}>
      <CardHeader className="flex-row items-center justify-between space-y-0 pb-3">
        <CardTitle className="text-sm">{title}</CardTitle>
        <Button
          size="sm"
          variant="outline"
          disabled={disabled}
          onClick={() => {
            setValue('');
            setAddOpen(true);
          }}
        >
          <Plus /> เพิ่ม
        </Button>
      </CardHeader>
      <CardContent className="flex-1 space-y-1 pt-0">
        {disabled && <p className="py-6 text-center text-sm text-muted-foreground">{disabledHint}</p>}
        {!disabled &&
          items?.map((it) => (
            <div
              key={it.id}
              className={cn(
                'group flex items-center gap-1 rounded-md px-2 py-1.5 text-sm',
                selectable && 'cursor-pointer',
                selectedId === it.id ? 'bg-accent text-accent-foreground' : 'hover:bg-muted',
              )}
              onClick={() => selectable && onSelect(it.id)}
            >
              <span className="flex-1 truncate">{it.label}</span>
              <button
                className="invisible rounded p-1 text-muted-foreground hover:text-foreground group-hover:visible"
                onClick={(e) => {
                  e.stopPropagation();
                  setValue(it.label);
                  setEditItem(it);
                }}
                aria-label="แก้ไข"
              >
                <Pencil className="h-3.5 w-3.5" />
              </button>
              <button
                className="invisible rounded p-1 text-muted-foreground hover:text-destructive group-hover:visible"
                onClick={(e) => {
                  e.stopPropagation();
                  setDeleteItem(it);
                }}
                aria-label="ลบ"
              >
                <Trash2 className="h-3.5 w-3.5" />
              </button>
              {selectable && <ChevronRight className="h-3.5 w-3.5 text-muted-foreground" />}
            </div>
          ))}
        {!disabled && items?.length === 0 && (
          <p className="py-6 text-center text-sm text-muted-foreground">ยังไม่มีข้อมูล</p>
        )}
      </CardContent>

      {/* Add */}
      <Dialog open={addOpen} onOpenChange={setAddOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>เพิ่ม{title}</DialogTitle>
          </DialogHeader>
          <div className="space-y-2">
            <Label htmlFor="add-val" required>{fieldLabel}</Label>
            <Input id="add-val" type={inputType} value={value} onChange={(e) => setValue(e.target.value)} />
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setAddOpen(false)}>
              ยกเลิก
            </Button>
            <Button
              disabled={busy || !value}
              onClick={() => run(() => onAdd(value), 'เพิ่มแล้ว', () => setAddOpen(false))}
            >
              บันทึก
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Edit */}
      <Dialog open={!!editItem} onOpenChange={(o) => !o && setEditItem(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>แก้ไข{title}</DialogTitle>
          </DialogHeader>
          <div className="space-y-2">
            <Label htmlFor="edit-val" required>{fieldLabel}</Label>
            <Input id="edit-val" type={inputType} value={value} onChange={(e) => setValue(e.target.value)} />
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setEditItem(null)}>
              ยกเลิก
            </Button>
            <Button
              disabled={busy || !value}
              onClick={() => {
                if (!editItem) return;
                run(() => onEdit(editItem.id, value), 'บันทึกแล้ว', () => setEditItem(null));
              }}
            >
              บันทึก
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Delete */}
      <Dialog open={!!deleteItem} onOpenChange={(o) => !o && setDeleteItem(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>ลบ{title}</DialogTitle>
            <DialogDescription>ต้องการลบ “{deleteItem?.label}” ใช่หรือไม่?</DialogDescription>
          </DialogHeader>
          <DialogFooter>
            <Button variant="outline" onClick={() => setDeleteItem(null)}>
              ยกเลิก
            </Button>
            <Button
              variant="destructive"
              disabled={busy}
              onClick={() => {
                if (!deleteItem) return;
                run(() => onDelete(deleteItem.id), 'ลบแล้ว', () => setDeleteItem(null));
              }}
            >
              ลบ
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </Card>
  );
}
