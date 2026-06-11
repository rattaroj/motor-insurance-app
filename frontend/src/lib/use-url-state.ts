'use client';

import { useCallback, useEffect, useState } from 'react';
import { usePathname, useRouter, useSearchParams } from 'next/navigation';
import { useDebouncedValue } from '@/lib/use-debounced';

/**
 * Query-string-backed state, so refresh / back / shared links restore the same view.
 * Values equal to their default are removed from the URL to keep it clean.
 *
 * NOTE: uses useSearchParams — the page's default export must be wrapped in <Suspense>.
 */
export function useUrlParams(): [URLSearchParams, (patch: Record<string, string | null>) => void] {
  const router = useRouter();
  const pathname = usePathname();
  const searchParams = useSearchParams();

  const patch = useCallback(
    (changes: Record<string, string | null>) => {
      const params = new URLSearchParams(searchParams.toString());
      for (const [key, value] of Object.entries(changes)) {
        if (value === null || value === '') params.delete(key);
        else params.set(key, value);
      }
      const qs = params.toString();
      router.replace(qs ? `${pathname}?${qs}` : pathname, { scroll: false });
    },
    [pathname, router, searchParams],
  );

  return [searchParams, patch];
}

/**
 * List-page state (page number, debounced search box, "all"-defaulted filters)
 * persisted in the query string: `?q=สมชาย&page=2&status=Active`.
 */
export function useListUrlState(filterKeys: string[] = []) {
  const [sp, patch] = useUrlParams();

  const page = Math.max(1, Number(sp.get('page')) || 1);
  const setPage = useCallback((p: number) => patch({ page: p > 1 ? String(p) : null }), [patch]);

  const [searchInput, setSearchInput] = useState(sp.get('q') ?? '');
  const search = useDebouncedValue(searchInput, 300);
  // Push the settled search term to the URL and reset the page — skips mount
  // (and restored links) because the URL already matches the input then.
  useEffect(() => {
    if ((sp.get('q') ?? '') !== search) patch({ q: search || null, page: null });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [search]);

  const filters: Record<string, string> = {};
  for (const key of filterKeys) filters[key] = sp.get(key) ?? 'all';
  const setFilter = useCallback(
    (key: string, value: string) => patch({ [key]: value === 'all' ? null : value, page: null }),
    [patch],
  );

  return { page, setPage, searchInput, onSearchChange: setSearchInput, search, filters, setFilter };
}
