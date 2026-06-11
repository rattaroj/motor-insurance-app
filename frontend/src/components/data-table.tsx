'use client';

import { Search, Inbox, ChevronLeft, ChevronRight } from 'lucide-react';
import { Card } from '@/components/ui/card';
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from '@/components/ui/table';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import { cn } from '@/lib/utils';

export type Column<T> = {
  header: React.ReactNode;
  cell: (row: T) => React.ReactNode;
  /** Applied to both the header and the body cell (e.g. "text-right"). */
  className?: string;
};

interface DataTableProps<T> {
  columns: Column<T>[];
  rows: T[] | undefined;
  getKey: (row: T) => React.Key;
  loading?: boolean;
  // server-side pagination (controlled)
  page: number;
  pageSize: number;
  totalCount: number;
  onPageChange: (page: number) => void;
  // search (controlled, optional)
  search?: string;
  onSearchChange?: (value: string) => void;
  searchPlaceholder?: string;
  // extra filter controls next to the search box
  toolbar?: React.ReactNode;
  emptyText?: string;
}

export function DataTable<T>({
  columns,
  rows,
  getKey,
  loading = false,
  page,
  pageSize,
  totalCount,
  onPageChange,
  search,
  onSearchChange,
  searchPlaceholder = 'ค้นหา…',
  toolbar,
  emptyText = 'ไม่มีข้อมูล',
}: DataTableProps<T>) {
  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize));
  const isEmpty = !loading && (rows?.length ?? 0) === 0;
  // While loading, fill the body with shimmering placeholder rows so the table keeps its height.
  const skeletonRows = Math.min(pageSize, 8);

  return (
    <div className="space-y-3">
      {(onSearchChange || toolbar) && (
        <div className="flex flex-wrap items-center gap-2">
          {onSearchChange && (
            <div className="relative w-full max-w-xs">
              <Search className="absolute left-2.5 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
              <Input
                value={search ?? ''}
                onChange={(e) => onSearchChange(e.target.value)}
                placeholder={searchPlaceholder}
                className="pl-8"
              />
            </div>
          )}
          {toolbar}
        </div>
      )}

      <Card className="overflow-hidden">
        <Table>
          <TableHeader>
            <TableRow className="bg-muted/50 hover:bg-muted/50">
              {columns.map((c, i) => (
                <TableHead key={i} className={c.className}>
                  {c.header}
                </TableHead>
              ))}
            </TableRow>
          </TableHeader>
          <TableBody>
            {loading &&
              Array.from({ length: skeletonRows }).map((_, r) => (
                <TableRow key={`skeleton-${r}`}>
                  {columns.map((c, i) => (
                    <TableCell key={i} className={c.className}>
                      <Skeleton className={cn('h-4 w-2/3', c.className?.includes('text-right') && 'ml-auto')} />
                    </TableCell>
                  ))}
                </TableRow>
              ))}
            {!loading &&
              rows?.map((r) => (
                <TableRow key={getKey(r)}>
                  {columns.map((c, i) => (
                    <TableCell key={i} className={c.className}>
                      {c.cell(r)}
                    </TableCell>
                  ))}
                </TableRow>
              ))}
            {isEmpty && (
              <TableRow className="hover:bg-transparent">
                <TableCell colSpan={columns.length}>
                  <div className="flex flex-col items-center gap-2 py-10 text-muted-foreground">
                    <span className="flex h-12 w-12 items-center justify-center rounded-full bg-muted">
                      <Inbox className="h-6 w-6 text-muted-foreground/60" />
                    </span>
                    <span className="text-sm">{emptyText}</span>
                  </div>
                </TableCell>
              </TableRow>
            )}
          </TableBody>
        </Table>
      </Card>

      <TablePagination page={page} totalPages={totalPages} totalCount={totalCount} onPageChange={onPageChange} />
    </div>
  );
}

/** Numbered pagination footer shared by DataTable and hand-rolled tables. */
export function TablePagination({
  page,
  totalPages,
  totalCount,
  onPageChange,
}: {
  page: number;
  totalPages: number;
  totalCount: number;
  onPageChange: (page: number) => void;
}) {
  const current = Math.min(page, totalPages);
  return (
    <div className="flex flex-wrap items-center justify-between gap-2 text-sm text-muted-foreground">
      <span>ทั้งหมด {totalCount} รายการ</span>
      <div className="flex items-center gap-1">
        <Button
          variant="ghost"
          size="sm"
          className="h-8 w-8 p-0"
          aria-label="หน้าก่อนหน้า"
          disabled={page <= 1}
          onClick={() => onPageChange(page - 1)}
        >
          <ChevronLeft />
        </Button>
        {pageItems(current, totalPages).map((item, i) =>
          item === '…' ? (
            <span key={`gap-${i}`} className="px-1.5 text-muted-foreground/60">
              …
            </span>
          ) : (
            <Button
              key={item}
              variant={item === current ? 'default' : 'ghost'}
              size="sm"
              className="h-8 w-8 p-0 tabular-nums"
              aria-current={item === current ? 'page' : undefined}
              onClick={() => onPageChange(item)}
            >
              {item}
            </Button>
          ),
        )}
        <Button
          variant="ghost"
          size="sm"
          className="h-8 w-8 p-0"
          aria-label="หน้าถัดไป"
          disabled={page >= totalPages}
          onClick={() => onPageChange(page + 1)}
        >
          <ChevronRight />
        </Button>
      </div>
    </div>
  );
}

/** Page numbers around the current page, with ellipsis gaps (e.g. 1 … 4 5 6 … 20). */
function pageItems(current: number, total: number): (number | '…')[] {
  if (total <= 7) return Array.from({ length: total }, (_, i) => i + 1);
  if (current <= 4) return [1, 2, 3, 4, 5, '…', total];
  if (current >= total - 3) return [1, '…', total - 4, total - 3, total - 2, total - 1, total];
  return [1, '…', current - 1, current, current + 1, '…', total];
}
