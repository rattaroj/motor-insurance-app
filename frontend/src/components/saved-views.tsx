'use client';

import { useEffect, useRef, useState } from 'react';
import { usePathname, useRouter, useSearchParams } from 'next/navigation';
import { Bookmark, Check, Plus, Trash2, X } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { useSavedViews } from '@/lib/use-saved-views';
import { cn } from '@/lib/utils';

/**
 * Dropdown to save the current list filters/search as a named view and re-apply saved ones.
 * Reads the current query string from the URL ([[use-url-state]]) and persists per `pageKey`.
 */
export function SavedViews({ pageKey }: { pageKey: string }) {
  const router = useRouter();
  const pathname = usePathname();
  const searchParams = useSearchParams();
  const currentQuery = searchParams.toString();
  const { views, save, remove } = useSavedViews(pageKey);

  const [open, setOpen] = useState(false);
  const [name, setName] = useState('');
  const ref = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const onClick = (e: MouseEvent) => {
      if (ref.current && !ref.current.contains(e.target as Node)) setOpen(false);
    };
    document.addEventListener('mousedown', onClick);
    return () => document.removeEventListener('mousedown', onClick);
  }, []);

  const apply = (query: string) => {
    router.replace(query ? `${pathname}?${query}` : pathname, { scroll: false });
    setOpen(false);
  };

  const saveCurrent = () => {
    save(name, currentQuery);
    setName('');
  };

  const activeName = views.find((v) => v.query === currentQuery)?.name;

  return (
    <div ref={ref} className="relative">
      <Button variant="outline" onClick={() => setOpen((o) => !o)}>
        <Bookmark className={cn('h-4 w-4', activeName && 'fill-primary text-primary')} />
        {activeName ?? 'มุมมอง'}
      </Button>

      {open && (
        <div className="absolute right-0 z-50 mt-2 w-72 overflow-hidden rounded-md border bg-popover p-1 shadow-md">
          {views.length === 0 ? (
            <p className="px-3 py-2 text-xs text-muted-foreground">ยังไม่มีมุมมองที่บันทึกไว้</p>
          ) : (
            <ul className="max-h-64 overflow-auto py-1">
              {views.map((v) => {
                const active = v.query === currentQuery;
                return (
                  <li key={v.name} className="flex items-center">
                    <button
                      type="button"
                      onClick={() => apply(v.query)}
                      className={cn(
                        'flex flex-1 items-center gap-2 rounded-sm px-3 py-2 text-left text-sm hover:bg-accent',
                        active && 'text-primary',
                      )}
                    >
                      {active ? <Check className="h-3.5 w-3.5 shrink-0" /> : <Bookmark className="h-3.5 w-3.5 shrink-0 text-muted-foreground" />}
                      <span className="truncate">{v.name}</span>
                    </button>
                    <button
                      type="button"
                      aria-label={`ลบมุมมอง ${v.name}`}
                      onClick={() => remove(v.name)}
                      className="mr-1 rounded-sm p-1.5 text-muted-foreground hover:bg-destructive/10 hover:text-destructive"
                    >
                      <Trash2 className="h-3.5 w-3.5" />
                    </button>
                  </li>
                );
              })}
            </ul>
          )}

          <div className="mt-1 border-t p-2">
            <p className="mb-1.5 px-1 text-[11px] font-medium uppercase tracking-wide text-muted-foreground/70">
              บันทึกมุมมองปัจจุบัน
            </p>
            <div className="flex items-center gap-1.5">
              <Input
                value={name}
                onChange={(e) => setName(e.target.value)}
                onKeyDown={(e) => e.key === 'Enter' && saveCurrent()}
                placeholder="ตั้งชื่อมุมมอง"
                className="h-8"
              />
              <Button size="sm" className="h-8 shrink-0 px-2" disabled={!name.trim()} onClick={saveCurrent}>
                <Plus />
              </Button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
