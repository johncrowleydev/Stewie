/**
 * FormGroup — labeled form field wrapper that pairs a label with a form control.
 *
 * Wraps the `formGroup` and `formLabel` class strings from `tw.ts` into a
 * composable container that connects a `<label>` to its child control via
 * the `htmlFor` attribute.
 *
 * **Design decisions:**
 * - Children-based composition rather than rendering an Input internally,
 *   so consumers can wrap any form control (Input, Select, textarea, etc.)
 *   (GOV-003 §8.2 — composability over coupling).
 * - `required` prop adds a visual asterisk indicator. This is purely visual —
 *   actual form validation is the consumer's responsibility.
 * - Uses `formGroup` and `formLabel` from tw.ts for consistent spacing and
 *   typography with the existing codebase.
 * - `data-testid` ensures deterministic test selectors (GOV-003 §8.4).
 *
 * Used by: LoginPage, RegisterPage, CreateJobPage, SettingsPage
 * Related: tw.ts (formGroup, formLabel), Input component, GOV-003 §8
 *
 * REF: JOB-028 T-502
 *
 * @example
 * ```tsx
 * <FormGroup label="Email" htmlFor="email" required>
 *   <Input id="email" type="email" placeholder="you@example.com" />
 * </FormGroup>
 * ```
 */

import { formGroup, formLabel } from "../../tw";

/**
 * Props for the {@link FormGroup} component.
 */
export interface FormGroupProps {
  /** Human-readable label text displayed above the form control. */
  label: string;
  /** The `id` of the child form control, used for the `<label>` `htmlFor` attribute. */
  htmlFor: string;
  /** The form control element(s) to render inside the group. */
  children: React.ReactNode;
  /** When true, displays a red asterisk after the label text. */
  required?: boolean;
}

/**
 * Renders a labeled form field group connecting a label to its child control.
 *
 * @returns A `<div>` containing a `<label>` and the provided children.
 */
export function FormGroup({
  label,
  htmlFor,
  children,
  required = false,
}: FormGroupProps): React.JSX.Element {
  return (
    <div className={formGroup} data-testid={`form-group-${htmlFor}`}>
      <label htmlFor={htmlFor} className={formLabel}>
        {label}
        {required && (
          <span className="text-ds-failed ml-xs" aria-hidden="true">
            *
          </span>
        )}
      </label>
      {children}
    </div>
  );
}
