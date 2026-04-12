---
id: JOB-028
title: "Reusable Component Library"
type: planning
status: OPEN
owner: architect
agents: [developer]
tags: [frontend, design-system, tailwind, components, phase-7]
related: [JOB-027, PRJ-001, GOV-003]
created: 2026-04-12
updated: 2026-04-12
version: 1.0.0
---

> **BLUF:** Convert the `tw.ts` string constants from JOB-027 into proper React components with typed props, variants, and composable APIs. Each task is ONE component — no batching, no deferral.

# JOB-028: Reusable Component Library

## Context

JOB-027 migrated all CSS to Tailwind utilities and created `tw.ts` as a bridge — shared class strings that pages import. This works but has limitations:

- No TypeScript props — consumers must manually compose class strings
- No variant system — button sizes, card styles, etc. are ad-hoc
- No slot/children patterns — cards and forms are just className strings
- No centralized testing surface

This job converts the most-used patterns into proper `<Button>`, `<Card>`, `<Input>`, etc. React components.

## Design Decisions

1. **Location:** All components go in `src/components/ui/` to distinguish from feature components (ChatPanel, ArchitectControls, etc.)
2. **Naming:** PascalCase files matching component name — `Button.tsx`, `Card.tsx`, etc.
3. **Barrel export:** `src/components/ui/index.ts` re-exports all components
4. **Props pattern:** Each component gets an explicit `interface {ComponentName}Props` — no `any`, no `Record<string, unknown>`
5. **tw.ts lifecycle:** Keep `tw.ts` during this sprint. Components import from it internally. Consumers gradually migrate to components. Full `tw.ts` deprecation is a separate job.
6. **Styling:** All components use ds-* @theme tokens from `app.css`. No hardcoded hex values.
7. **Dark mode:** All components must render correctly in both themes.

## Branch

`feature/JOB-028-component-library`

---

## Tasks

> [!IMPORTANT]
> **One component per task. One commit per task.** No batching. Each task is independently reviewable. Do NOT skip ahead or combine tasks.

### T-500: Button Component

**File:** `src/components/ui/Button.tsx`

Create a `<Button>` component with the following API:

```tsx
interface ButtonProps extends React.ButtonHTMLAttributes<HTMLButtonElement> {
  variant: 'primary' | 'ghost' | 'danger';
  size?: 'sm' | 'md';          // default: 'md'
  loading?: boolean;            // shows spinner, disables click
  as?: 'button' | 'a';         // renders as <a> when 'a'
  href?: string;                // only when as='a'
}
```

- Import base styles from `tw.ts` (btnPrimary, btnGhost, btnDanger)
- Add size variants (sm = smaller padding/text)
- Loading state: disabled + spinner SVG
- `as='a'` renders a React Router `<Link>` for internal hrefs
- Dark mode outline variant for primary (already in tw.ts)

**Acceptance criteria:**
- `<Button variant="primary">Save</Button>` renders identically to current `btnPrimary` usage
- `<Button variant="primary" loading>Saving…</Button>` shows spinner, is disabled
- All 3 variants render correctly in light and dark mode
- TypeScript: no `any`, explicit return type
- JSDoc on component and props interface

**Commit:** `feat(JOB-028): add Button component (T-500)`

---

### T-501: Card Component

**File:** `src/components/ui/Card.tsx`

```tsx
interface CardProps {
  children: React.ReactNode;
  className?: string;           // merge with base
  hoverable?: boolean;          // adds hover:shadow-ds-md
  padding?: 'sm' | 'md' | 'lg'; // default: 'lg' (matches current)
}
```

- Import base style from `tw.ts` (card)
- Optional header slot via `Card.Header` compound component
- Optional footer slot via `Card.Footer`

**Acceptance criteria:**
- `<Card>content</Card>` renders identically to current `card` usage
- Hoverable variant adds shadow on hover
- Compound pattern works: `<Card><Card.Header>Title</Card.Header>body</Card>`
- Both themes render correctly

**Commit:** `feat(JOB-028): add Card component (T-501)`

---

### T-502: Input + FormGroup Components

**Files:** `src/components/ui/Input.tsx`, `src/components/ui/FormGroup.tsx`

```tsx
interface InputProps extends React.InputHTMLAttributes<HTMLInputElement> {
  error?: string;               // shows error text below
  hint?: string;                // shows hint text below
}

interface FormGroupProps {
  label: string;
  htmlFor: string;
  children: React.ReactNode;
  required?: boolean;
}
```

- Import base styles from `tw.ts` (formInput, formLabel, formGroup, formHint)
- Error state: red border + error message
- Composed usage: `<FormGroup label="Name" htmlFor="name"><Input id="name" /></FormGroup>`

**Acceptance criteria:**
- `<Input />` renders identically to current `formInput` usage
- Error state shows red border and message
- FormGroup wraps label + input correctly
- Both themes render correctly

**Commit:** `feat(JOB-028): add Input and FormGroup components (T-502)`

---

### T-503: Badge / StatusBadge Enhancement

**File:** Modify existing `src/components/StatusBadge.tsx` → move to `src/components/ui/Badge.tsx`

```tsx
interface BadgeProps {
  variant: 'pending' | 'running' | 'completed' | 'failed' | 'warning' | 'info';
  size?: 'sm' | 'md';
  dot?: boolean;                // animated dot indicator (default: true)
  children: React.ReactNode;
}
```

- Current StatusBadge is 44 lines with hardcoded status→color mapping
- Refactor to generic Badge with variant-based styling
- Export as `Badge` (rename from StatusBadge)
- Update ALL existing `<StatusBadge>` consumers to use new `<Badge>` import

**Acceptance criteria:**
- All existing status badge usages still render identically
- New `info` and `warning` variants work
- Size variant works
- Consumer files updated (DashboardPage, JobsPage, JobDetailPage)
- Both themes render correctly

**Commit:** `feat(JOB-028): refactor StatusBadge to Badge component (T-503)`

---

### T-504: DataTable Component

**File:** `src/components/ui/DataTable.tsx`

```tsx
interface Column<T> {
  key: string;
  header: string;
  render?: (row: T) => React.ReactNode;
  width?: string;
}

interface DataTableProps<T> {
  columns: Column<T>[];
  data: T[];
  onRowClick?: (row: T) => void;
  emptyMessage?: string;
  loading?: boolean;            // shows skeleton rows
  skeletonRows?: number;        // default: 3
}
```

- Import base styles from `tw.ts` (dataTable, th, td, trClickable, emptyState, skeleton)
- Generic type parameter for row data
- Built-in empty state and loading skeleton
- Clickable rows with hover effect when `onRowClick` provided

**Acceptance criteria:**
- `<DataTable columns={...} data={jobs} />` renders identically to current job table on DashboardPage
- Empty state renders when data is empty
- Loading skeleton renders when `loading={true}`
- Row click handler fires correctly
- Both themes render correctly

**Commit:** `feat(JOB-028): add DataTable component (T-504)`

---

### T-505: Select / Dropdown Component

**File:** `src/components/ui/Select.tsx`

```tsx
interface SelectOption {
  value: string;
  label: string;
  icon?: React.ReactNode;
}

interface SelectProps {
  options: SelectOption[];
  value: string;
  onChange: (value: string) => void;
  placeholder?: string;
  label?: string;
}
```

- Custom dropdown (not native `<select>`) using the existing `dropdown-appear` keyframe
- Keyboard navigation: arrow keys, enter, escape
- Click-outside to close
- Used by ArchitectControls (model/provider selector) and filter buttons

**Acceptance criteria:**
- Opens/closes correctly with animation
- Keyboard navigation works (up, down, enter, escape)
- Click-outside closes
- Selected value displays correctly
- Both themes render correctly

**Commit:** `feat(JOB-028): add Select component (T-505)`

---

### T-506: Modal Component

**File:** `src/components/ui/Modal.tsx`

```tsx
interface ModalProps {
  isOpen: boolean;
  onClose: () => void;
  title: string;
  children: React.ReactNode;
  footer?: React.ReactNode;
  size?: 'sm' | 'md' | 'lg';   // default: 'md'
}
```

- Backdrop with `overlay-fade-in` animation
- Focus trap (tab cycles within modal)
- Escape key closes
- Body scroll lock when open
- Currently no modals exist — this is forward-looking for JOB-029 (delete confirmations) and JOB-033 (admin panels)

**Acceptance criteria:**
- Opens/closes with animation
- Focus trap works correctly
- Escape closes modal
- Backdrop click closes modal
- Three sizes render correctly
- Both themes render correctly

**Commit:** `feat(JOB-028): add Modal component (T-506)`

---

### T-507: Barrel Export + Documentation

**File:** `src/components/ui/index.ts`

- Create barrel export: `export { Button } from './Button'` etc.
- Add JSDoc module comment documenting the component library
- Do NOT migrate existing page consumers to new components yet (that's JOB-029+)

**Acceptance criteria:**
- `import { Button, Card, Input, FormGroup, Badge, DataTable, Select, Modal } from '../components/ui'` works
- `npm run build` succeeds with zero errors
- No unused exports or imports

**Commit:** `feat(JOB-028): add barrel export and finalize component library (T-507)`

---

## Exit Criteria

- [ ] All 7 components created in `src/components/ui/`
- [ ] Barrel export in `src/components/ui/index.ts`
- [ ] `npm run build` succeeds with zero errors
- [ ] Every component renders correctly in both light and dark mode
- [ ] Every component has TypeScript props interface — no `any`
- [ ] Every component has JSDoc documentation
- [ ] One commit per task (7 commits total)
