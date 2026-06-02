'use client';

import * as React from 'react';
import { createPortal } from 'react-dom';
import { Check, ChevronsUpDown, Loader2, Search } from 'lucide-react';
import { cn } from '@/lib/utils';

export interface ComboboxOption {
  value: number;
  label: string;
  sublabel?: string;
}

interface ComboboxProps {
  value: number | null;
  onChange: (value: number | null) => void;
  options: ComboboxOption[];
  placeholder?: string;
  searchPlaceholder?: string;
  emptyText?: string;
  disabled?: boolean;
  loading?: boolean;
  /** How many rows to render per lazy "page" (grows on scroll). */
  pageSize?: number;
  className?: string;
  id?: string;
}

interface PanelStyle {
  left: number;
  width: number;
  top?: number;
  bottom?: number;
  maxHeight: number;
}

/**
 * Searchable single-select with client-side filtering and lazy (scroll-paged) rendering.
 * The dropdown panel is rendered in a portal with fixed positioning so it is never clipped
 * by a scrollable/overflow-hidden ancestor (e.g. a dialog); it tracks the trigger on
 * scroll/resize and flips above when there is more room there. Closes on outside-click / Escape.
 */
export function Combobox({
  value,
  onChange,
  options,
  placeholder = 'เลือก…',
  searchPlaceholder = 'ค้นหา…',
  emptyText = 'ไม่พบรายการ',
  disabled,
  loading,
  pageSize = 50,
  className,
  id,
}: ComboboxProps) {
  const [open, setOpen] = React.useState(false);
  const [search, setSearch] = React.useState('');
  const [visible, setVisible] = React.useState(pageSize);
  const [panel, setPanel] = React.useState<PanelStyle | null>(null);
  const triggerRef = React.useRef<HTMLButtonElement>(null);
  const panelRef = React.useRef<HTMLDivElement>(null);
  const listRef = React.useRef<HTMLDivElement>(null);
  const searchRef = React.useRef<HTMLInputElement>(null);

  const selected = React.useMemo(() => options.find((o) => o.value === value) ?? null, [options, value]);

  const filtered = React.useMemo(() => {
    const q = search.trim().toLowerCase();
    if (!q) return options;
    return options.filter(
      (o) => o.label.toLowerCase().includes(q) || (o.sublabel?.toLowerCase().includes(q) ?? false),
    );
  }, [options, search]);

  // Position the portal panel relative to the trigger, flipping up when needed.
  const reposition = React.useCallback(() => {
    const el = triggerRef.current;
    if (!el) return;
    const r = el.getBoundingClientRect();
    const spaceBelow = window.innerHeight - r.bottom - 8;
    const spaceAbove = r.top - 8;
    const up = spaceBelow < 260 && spaceAbove > spaceBelow;
    const maxHeight = Math.max(160, Math.min(320, up ? spaceAbove : spaceBelow));
    setPanel(
      up
        ? { left: r.left, width: r.width, bottom: window.innerHeight - r.top + 4, maxHeight }
        : { left: r.left, width: r.width, top: r.bottom + 4, maxHeight },
    );
  }, []);

  // Reset the lazy window whenever the result set or open-state changes.
  React.useEffect(() => {
    setVisible(pageSize);
    if (listRef.current) listRef.current.scrollTop = 0;
  }, [search, open, pageSize]);

  React.useLayoutEffect(() => {
    if (open) reposition();
  }, [open, reposition]);

  // Focus search on open; keep the panel pinned to the trigger on scroll/resize.
  React.useEffect(() => {
    if (!open) return;
    searchRef.current?.focus();
    const onMove = () => reposition();
    window.addEventListener('scroll', onMove, true);
    window.addEventListener('resize', onMove);
    return () => {
      window.removeEventListener('scroll', onMove, true);
      window.removeEventListener('resize', onMove);
    };
  }, [open, reposition]);

  // Close on outside click / Escape (the panel lives in a portal, so check it too).
  React.useEffect(() => {
    if (!open) return;
    const onDown = (e: MouseEvent) => {
      const t = e.target as Node;
      if (triggerRef.current?.contains(t) || panelRef.current?.contains(t)) return;
      setOpen(false);
    };
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') setOpen(false);
    };
    document.addEventListener('mousedown', onDown);
    document.addEventListener('keydown', onKey);
    return () => {
      document.removeEventListener('mousedown', onDown);
      document.removeEventListener('keydown', onKey);
    };
  }, [open]);

  const onScroll = (e: React.UIEvent<HTMLDivElement>) => {
    const el = e.currentTarget;
    if (el.scrollHeight - el.scrollTop - el.clientHeight < 80) {
      setVisible((v) => (v < filtered.length ? v + pageSize : v));
    }
  };

  const pick = (v: number) => {
    onChange(v === value ? null : v);
    setOpen(false);
    setSearch('');
  };

  const shown = filtered.slice(0, visible);

  return (
    <div className={cn('relative', className)}>
      <button
        type="button"
        id={id}
        ref={triggerRef}
        disabled={disabled}
        onClick={() => setOpen((o) => !o)}
        className={cn(
          'flex h-10 w-full items-center justify-between rounded-md border border-input bg-background px-3 py-2 text-sm ring-offset-background focus:outline-none focus:ring-2 focus:ring-ring focus:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50',
        )}
      >
        <span className={cn('line-clamp-1 text-left', !selected && 'text-muted-foreground')}>
          {selected ? selected.label : placeholder}
        </span>
        {loading ? (
          <Loader2 className="h-4 w-4 shrink-0 animate-spin opacity-50" />
        ) : (
          <ChevronsUpDown className="h-4 w-4 shrink-0 opacity-50" />
        )}
      </button>

      {open &&
        panel &&
        typeof document !== 'undefined' &&
        createPortal(
          <div
            ref={panelRef}
            style={{
              position: 'fixed',
              left: panel.left,
              width: panel.width,
              top: panel.top,
              bottom: panel.bottom,
              maxHeight: panel.maxHeight,
            }}
            className="z-50 flex flex-col overflow-hidden rounded-md border bg-popover text-popover-foreground shadow-md"
          >
            <div className="flex shrink-0 items-center border-b px-3">
              <Search className="h-4 w-4 shrink-0 opacity-50" />
              <input
                ref={searchRef}
                value={search}
                onChange={(e) => setSearch(e.target.value)}
                placeholder={searchPlaceholder}
                className="h-9 w-full bg-transparent px-2 text-sm outline-none placeholder:text-muted-foreground"
              />
            </div>
            <div ref={listRef} onScroll={onScroll} className="flex-1 overflow-y-auto p-1">
              {loading ? (
                <div className="flex items-center justify-center gap-2 py-6 text-sm text-muted-foreground">
                  <Loader2 className="h-4 w-4 animate-spin" /> กำลังโหลด…
                </div>
              ) : shown.length === 0 ? (
                <div className="py-6 text-center text-sm text-muted-foreground">{emptyText}</div>
              ) : (
                shown.map((o) => (
                  <button
                    type="button"
                    key={o.value}
                    onClick={() => pick(o.value)}
                    className={cn(
                      'flex w-full cursor-pointer select-none items-center justify-between gap-2 rounded-sm px-2 py-1.5 text-left text-sm outline-none hover:bg-accent hover:text-accent-foreground',
                      o.value === value && 'bg-accent/50',
                    )}
                  >
                    <span className="line-clamp-1">
                      {o.label}
                      {o.sublabel && <span className="ml-2 text-xs text-muted-foreground">{o.sublabel}</span>}
                    </span>
                    {o.value === value && <Check className="h-4 w-4 shrink-0" />}
                  </button>
                ))
              )}
              {!loading && visible < filtered.length && (
                <div className="py-2 text-center text-xs text-muted-foreground">
                  เลื่อนเพื่อโหลดเพิ่ม ({filtered.length - visible} รายการ)
                </div>
              )}
            </div>
          </div>,
          document.body,
        )}
    </div>
  );
}
