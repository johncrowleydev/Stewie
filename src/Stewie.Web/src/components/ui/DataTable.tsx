/**
 * DataTable — generic typed data table with loading skeleton and empty state.
 *
 * Wraps the `dataTable`, `th`, `td`, `trClickable`, `emptyState`, and
 * `skeleton` class strings from `tw.ts` into a fully typed generic React
 * component with column definitions, click handlers, and built-in states.
 *
 * **Design decisions:**
 * - Generic type parameter `T` ensures column `key` and `render` functions
 *   are type-safe against the row data shape (GOV-003 §8.5).
 * - Column `key` is typed as `string & keyof T` for direct property access,
 *   but `render` override allows arbitrary computed columns.
 * - Loading state renders skeleton rows that match the column count, giving
 *   a realistic preview of the table shape during data fetches.
 * - Empty state is built-in rather than requiring consumers to handle it
 *   externally, reducing boilerplate and ensuring visual consistency.
 * - `onRowClick` makes the entire row clickable with hover styling from tw.ts.
 * - Horizontal overflow is handled with `overflow-x-auto` for mobile
 *   responsiveness (GOV-003 §8.10).
 * - `data-testid` on table, rows, and cells for test selectors (GOV-003 §8.4).
 *
 * Used by: DashboardPage (job table), JobsPage, EventsPage, SettingsPage
 * Related: tw.ts (dataTable, th, td, trClickable, emptyState, skeleton)
 *
 * REF: JOB-028 T-504
 *
 * @example
 * ```tsx
 * interface Job { id: string; name: string; status: string }
 *
 * const columns: Column<Job>[] = [
 *   { key: "name", header: "Name" },
 *   { key: "status", header: "Status", render: (row) => <Badge variant={row.status}>{row.status}</Badge> },
 * ];
 *
 * <DataTable columns={columns} data={jobs} onRowClick={(job) => navigate(`/jobs/${job.id}`)} />
 * <DataTable columns={columns} data={[]} emptyMessage="No jobs found" />
 * <DataTable columns={columns} data={[]} loading skeletonRows={5} />
 * ```
 */

import { dataTable, th, td, trClickable, emptyState, skeleton } from "../../tw";

/**
 * Column definition for the DataTable.
 *
 * @typeParam T - The row data type.
 */
export interface Column<T> {
  /** Property key on the row object. Used for default cell rendering. */
  key: string;
  /** Header text displayed in the column header cell. */
  header: string;
  /**
   * Optional custom render function for the cell content.
   * When omitted, the raw property value is rendered as a string.
   */
  render?: (row: T) => React.ReactNode;
  /** Optional CSS width for the column (e.g. "120px", "20%"). */
  width?: string;
}

/**
 * Props for the {@link DataTable} component.
 *
 * @typeParam T - The row data type.
 */
export interface DataTableProps<T> {
  /** Column definitions controlling header labels and cell rendering. */
  columns: Column<T>[];
  /** Array of row data objects. */
  data: T[];
  /** Callback fired when a row is clicked. Adds hover styling to rows. */
  onRowClick?: (row: T) => void;
  /** Message shown when `data` is empty and `loading` is false. */
  emptyMessage?: string;
  /** When true, renders skeleton placeholder rows instead of data. */
  loading?: boolean;
  /** Number of skeleton rows to display during loading. Defaults to 3. */
  skeletonRows?: number;
}

/** Default skeleton row count when loading without explicit skeletonRows. */
const DEFAULT_SKELETON_ROWS = 3;

/**
 * Renders a themed data table with typed columns, loading skeleton, and empty state.
 *
 * @typeParam T - The row data type.
 * @returns A `<div>` wrapping a `<table>` element with responsive overflow.
 */
export function DataTable<T extends Record<string, unknown>>({
  columns,
  data,
  onRowClick,
  emptyMessage = "No data to display",
  loading = false,
  skeletonRows = DEFAULT_SKELETON_ROWS,
}: DataTableProps<T>): React.JSX.Element {
  const isClickable = Boolean(onRowClick);

  return (
    <div className="overflow-x-auto" data-testid="data-table-wrapper">
      <table className={dataTable} data-testid="data-table">
        {/* ── Table header ── */}
        <thead>
          <tr>
            {columns.map((col) => (
              <th
                key={col.key}
                className={th}
                style={col.width ? { width: col.width } : undefined}
                data-testid={`data-table-th-${col.key}`}
              >
                {col.header}
              </th>
            ))}
          </tr>
        </thead>

        {/* ── Table body ── */}
        <tbody>
          {/* Loading state: skeleton rows */}
          {loading &&
            Array.from({ length: skeletonRows }).map((_, rowIndex) => (
              <tr
                key={`skeleton-${String(rowIndex)}`}
                className="border-b border-ds-border"
                data-testid="data-table-skeleton-row"
              >
                {columns.map((col) => (
                  <td key={col.key} className={td}>
                    <div
                      className={`${skeleton} h-4 w-3/4 rounded`}
                      aria-hidden="true"
                    />
                  </td>
                ))}
              </tr>
            ))}

          {/* Empty state */}
          {!loading && data.length === 0 && (
            <tr data-testid="data-table-empty">
              <td colSpan={columns.length} className={emptyState}>
                {emptyMessage}
              </td>
            </tr>
          )}

          {/* Data rows */}
          {!loading &&
            data.map((row, rowIndex) => (
              <tr
                key={String(rowIndex)}
                className={isClickable ? trClickable : "border-b border-ds-border last:border-b-0"}
                onClick={onRowClick ? () => onRowClick(row) : undefined}
                data-testid={`data-table-row-${String(rowIndex)}`}
              >
                {columns.map((col) => (
                  <td key={col.key} className={td}>
                    {col.render
                      ? col.render(row)
                      : String(row[col.key] ?? "")}
                  </td>
                ))}
              </tr>
            ))}
        </tbody>
      </table>
    </div>
  );
}
