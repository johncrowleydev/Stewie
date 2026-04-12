/**
 * Input — styled text input with error/hint support.
 *
 * Wraps the `formInput` class string from `tw.ts` into a typed React
 * component with optional error and hint text display.
 *
 * **Design decisions:**
 * - Extends native `<input>` attributes so consumers pass `type`, `placeholder`,
 *   `onChange`, `value`, `aria-*`, etc. directly (GOV-003 §8.2).
 * - Error state applies a red border via `border-ds-failed` and renders the
 *   message below the input for immediate inline validation feedback.
 * - Hint text renders below the input in muted style for helper guidance.
 * - Error takes precedence over hint — if both are provided, only error shows.
 *   This prevents confusing duplicate messages.
 * - `forwardRef` is used so parent components (FormGroup, custom forms) can
 *   access the underlying `<input>` DOM node for focus management.
 * - `data-testid` ensures deterministic test selectors (GOV-003 §8.4).
 *
 * Used by: LoginPage, RegisterPage, SettingsPage
 * Related: tw.ts (formInput), FormGroup component, GOV-003 §8
 *
 * REF: JOB-028 T-502
 *
 * @example
 * ```tsx
 * <Input placeholder="Enter name" />
 * <Input error="Name is required" />
 * <Input hint="Must be at least 3 characters" />
 * ```
 */

import React from "react";
import { formInput } from "../../tw";

/**
 * Props for the {@link Input} component.
 *
 * Extends native `<input>` attributes for full HTML input compatibility.
 */
export interface InputProps
  extends React.InputHTMLAttributes<HTMLInputElement> {
  /** Error message displayed below the input. Adds red border styling. */
  error?: string;
  /** Hint/helper text displayed below the input in muted style. */
  hint?: string;
}

/**
 * Renders a themed text input with optional error/hint messages.
 *
 * Uses `forwardRef` for DOM access by parent components.
 *
 * @returns An `<input>` element with optional validation message.
 */
export const Input = React.forwardRef<HTMLInputElement, InputProps>(
  function Input(
    { error, hint, className, ...rest }: InputProps,
    ref: React.ForwardedRef<HTMLInputElement>,
  ): React.JSX.Element {
    const errorClass = error ? "border-ds-failed focus:border-ds-failed" : "";
    const composedClassName = [formInput, errorClass, className ?? ""]
      .filter(Boolean)
      .join(" ");

    /* DECISION: Error takes precedence over hint to prevent duplicate messages.
     * TRADEOFF: Consumers who want both visible simultaneously should use
     * custom layout rather than relying on this component's built-in slots. */
    const messageText = error ?? hint;
    const messageIsError = Boolean(error);

    return (
      <div>
        <input
          ref={ref}
          className={composedClassName}
          aria-invalid={messageIsError || undefined}
          aria-describedby={messageText ? `${rest.id}-message` : undefined}
          data-testid="input"
          {...rest}
        />
        {messageText && (
          <p
            id={rest.id ? `${rest.id}-message` : undefined}
            className={
              messageIsError
                ? "text-xs text-ds-failed mt-xs"
                : "text-xs text-ds-text-muted mt-xs italic"
            }
            data-testid={messageIsError ? "input-error" : "input-hint"}
            role={messageIsError ? "alert" : undefined}
          >
            {messageText}
          </p>
        )}
      </div>
    );
  },
);
