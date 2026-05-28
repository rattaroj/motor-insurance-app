'use client';

import { Search } from 'lucide-react';
import { Card } from '@/components/ui/card';
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from '@/components/ui/table';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';

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

      <Card>
        <Table>
          <TableHeader>
            <TableRow>
              {columns.map((c, i) => (
                <TableHead key={i} className={c.className}>
                  {c.header}
                </TableHead>
              ))}
            </TableRow>
          </TableHeader>
          <TableBody>
            {loading && (
              <TableRow>
                <TableCell colSpan={columns.length} className="text-center text-muted-foreground">
                  กำลังโหลด…
                </TableCell>
              </TableRow>
            )}
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
              <TableRow>
                <TableCell colSpan={columns.length} className="text-center text-muted-foreground">
                  {emptyText}
                </TableCell>
              </TableRow>
            )}
          </TableBody>
        </Table>
      </Card>

      <div className="flex items-center justify-between text-sm text-muted-foreground">
        <span>
          ทั้งหมด {totalCount} รายการ — หน้า {Math.min(page, totalPages)}/{totalPages}
        </span>
        <div className="flex gap-2">
          <Button variant="outline" size="sm" disabled={page <= 1} onClick={() => onPageChange(page - 1)}>
            ก่อนหน้า
          </Button>
          <Button variant="outline" size="sm" disabled={page >= totalPages} onClick={() => onPageChange(page + 1)}>
            ถัดไป
          </Button>
        </div>
      </div>
    </div>
  );
}
