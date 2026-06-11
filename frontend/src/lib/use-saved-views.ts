'use client';

import { useCallback, useEffect, useState } from 'react';

export interface SavedView {
  name: string;
  /** The list query string this view restores, e.g. "status=Active&q=สมชาย". */
  query: string;
}

const KEY = (pageKey: string) => `saved-views:${pageKey}`;

/**
 * Named filter/search presets per list page, persisted in localStorage. Builds on the
 * URL-synced list state ([[use-url-state]]): a view just captures the current query string
 * and re-applies it later.
 */
export function useSavedViews(pageKey: string) {
  const [views, setViews] = useState<SavedView[]>([]);

  // Load once on mount (localStorage is client-only).
  useEffect(() => {
    try {
      const raw = localStorage.getItem(KEY(pageKey));
      if (raw) setViews(JSON.parse(raw));
    } catch {
      // ignore malformed storage
    }
  }, [pageKey]);

  const persist = useCallback(
    (next: SavedView[]) => {
      setViews(next);
      try {
        localStorage.setItem(KEY(pageKey), JSON.stringify(next));
      } catch {
        // ignore quota / unavailable storage
      }
    },
    [pageKey],
  );

  const save = useCallback(
    (name: string, query: string) => {
      const trimmed = name.trim();
      if (!trimmed) return;
      // Replace an existing view with the same name, else append.
      const next = [...views.filter((v) => v.name !== trimmed), { name: trimmed, query }];
      persist(next);
    },
    [views, persist],
  );

  const remove = useCallback(
    (name: string) => persist(views.filter((v) => v.name !== name)),
    [views, persist],
  );

  return { views, save, remove };
}
