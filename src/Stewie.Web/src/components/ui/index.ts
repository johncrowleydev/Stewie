/**
 * Stewie UI Component Library — Barrel Export
 *
 * Re-exports all reusable UI components from `src/components/ui/`.
 * These components wrap the `tw.ts` class string constants into typed React
 * components with props-driven variants, accessibility attributes, and
 * design system token integration.
 *
 * **Usage:**
 * ```tsx
 * import { Button, Card, Input, FormGroup, Badge, DataTable, Select, Modal } from '../components/ui';
 * ```
 *
 * **Component inventory:**
 * - {@link Button} — primary/ghost/danger buttons with loading and link support
 * - {@link Card} — composable card with Header/Footer compound components
 * - {@link Input} — text input with error/hint validation messages
 * - {@link FormGroup} — labeled form field wrapper
 * - {@link Badge} — color-coded status pill with variants and dot indicator
 * - {@link DataTable} — generic typed data table with skeleton and empty state
 * - {@link Select} — custom dropdown with keyboard navigation
 * - {@link Modal} — accessible dialog with focus trap and scroll lock
 *
 * **Migration note:** The legacy `StatusBadge` component from
 * `src/components/StatusBadge.tsx` is now a thin wrapper around `Badge`.
 * New code should import `Badge` from this barrel directly.
 *
 * REF: JOB-028 T-507
 *
 * @module ui
 */

export { Button } from "./Button";
export type { ButtonProps } from "./Button";

export { Card } from "./Card";
export type { CardProps, CardSectionProps } from "./Card";

export { Input } from "./Input";
export type { InputProps } from "./Input";

export { FormGroup } from "./FormGroup";
export type { FormGroupProps } from "./FormGroup";

export { Badge } from "./Badge";
export type { BadgeProps, BadgeVariant } from "./Badge";

export { DataTable } from "./DataTable";
export type { DataTableProps, Column } from "./DataTable";

export { Select } from "./Select";
export type { SelectProps, SelectOption } from "./Select";

export { Modal } from "./Modal";
export type { ModalProps } from "./Modal";
