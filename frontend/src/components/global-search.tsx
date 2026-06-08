'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { Search, FileText, ShieldCheck, User } from 'lucide-react';
import { useGlobalSearchQuery, type SearchHit } from '@/lib/api/insuranceApi';
import { useDebouncedValue } from '@/lib/use-debounced';

const ICON = { policy: ShieldCheck, claim: FileText, customer: User };
const HREF: Record<SearchHit['type'], (id: number) => string> = {
  policy: (id) => `/policies/${id}`,
  customer: (id) => `/customers/${id}`,
  claim: () => `/claims`, // claims are managed from the list (no standalone detail route)
};

/** Header quick-find across policies, claims and customers. */
export function GlobalSearch() {
  const router = useRouter();
  const [term, setTerm] = useState('');
  const [open, setOpen] = useState(false);
  const q = useDebouncedValue(term.trim(), 250);

  const { data, isFetching } = useGlobalSearchQuery(q, { skip: q.length < 2 });
  const hits: SearchHit[] = data ? [...data.policies, ...data.claims, ...data.customers] : [];
  const showPanel = open && q.length >= 2;

  const go = (hit: SearchHit) => {
    setOpen(false);
    setTerm('');
    router.push(HREF[hit.type](hit.id));
  };

  return (
    <div className="relative w-full max-w-xs">
      <Search className="pointer-events-none absolute left-2.5 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
      <input
        type="search"
        value={term}
        onChange={(e) => {
          setTerm(e.target.value);
          setOpen(true);
        }}
        onFocus={() => setOpen(true)}
        // Delay so a click on a result registers before the panel closes.
        onBlur={() => setTimeout(() => setOpen(false), 150)}
        placeholder="ค้นหากรมธรรม์ / เคลม / ลูกค้า"
        className="h-9 w-full rounded-md border bg-background pl-8 pr-3 text-sm outline-none focus:ring-2 focus:ring-ring"
      />

      {showPanel && (
        <div className="absolute left-0 right-0 top-11 z-50 overflow-hidden rounded-md border bg-popover shadow-md">
          {isFetching && hits.length === 0 ? (
            <p className="px-3 py-3 text-sm text-muted-foreground">กำลังค้นหา…</p>
          ) : hits.length === 0 ? (
            <p className="px-3 py-3 text-sm text-muted-foreground">ไม่พบผลลัพธ์</p>
          ) : (
            <ul className="max-h-80 overflow-auto py-1">
              {hits.map((hit) => {
                const Icon = ICON[hit.type];
                return (
                  <li key={`${hit.type}-${hit.id}`}>
                    <button
                      type="button"
                      onMouseDown={(e) => e.preventDefault()} // keep focus until click handler runs
                      onClick={() => go(hit)}
                      className="flex w-full items-center gap-2.5 px-3 py-2 text-left text-sm hover:bg-muted"
                    >
                      <Icon className="h-4 w-4 shrink-0 text-muted-foreground" />
                      <span className="font-medium">{hit.title}</span>
                      <span className="truncate text-muted-foreground">{hit.subtitle}</span>
                    </button>
                  </li>
                );
              })}
            </ul>
          )}
        </div>
      )}
    </div>
  );
}
