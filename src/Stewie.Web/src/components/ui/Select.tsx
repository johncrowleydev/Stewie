/**
 * Select — custom dropdown component with keyboard navigation.
 *
 * Replaces native `<select>` with a styled dropdown that uses the existing
 * `dropdown-appear` CSS keyframe from `app.css` and design system tokens.
 *
 * **Design decisions:**
 * - Custom dropdown instead of native `<select>` to support icons in options,
 *   consistent cross-browser styling, and the existing `dropdown-appear` animation.
 * - Full keyboard navigation: ArrowUp/ArrowDown to navigate, Enter/Space to
 *   select, Escape to close, Tab to move focus out (GOV-003 §8.3).
 * - Click-outside-to-close uses a `mousedown` listener on `document` to
 *   catch clicks before they propagate. Cleanup is in useEffect return.
 * - Focus management: opening the dropdown focuses the listbox; closing returns
 *   focus to the trigger button.
 * - ARIA: `role="listbox"`, `role="option"`, `aria-expanded`, `aria-selected`,
 *   `aria-activedescendant` for screen reader compatibility (GOV-003 §8.3).
 * - `data-testid` on trigger, listbox, and options (GOV-003 §8.4).
 *
 * Used by: ArchitectControls (model/provider selector), filter dropdowns
 * Related: app.css (dropdown-appear keyframe), GOV-003 §8
 *
 * REF: JOB-028 T-505
 *
 * @example
 * ```tsx
 * const models = [
 *   { value: "claude-4", label: "Claude 4" },
 *   { value: "gpt-5", label: "GPT-5", icon: <SparkleIcon /> },
 * ];
 *
 * <Select
 *   options={models}
 *   value={selectedModel}
 *   onChange={setSelectedModel}
 *   placeholder="Choose model…"
 *   label="AI Model"
 * />
 * ```
 */

import { useCallback, useEffect, useRef, useState } from "react";

/**
 * A single option in the Select dropdown.
 */
export interface SelectOption {
  /** The value submitted when this option is selected. */
  value: string;
  /** Human-readable label displayed in the dropdown. */
  label: string;
  /** Optional icon rendered before the label. */
  icon?: React.ReactNode;
}

/**
 * Props for the {@link Select} component.
 */
export interface SelectProps {
  /** Available options to display in the dropdown. */
  options: SelectOption[];
  /** Currently selected value. Must match an option's `value` field. */
  value: string;
  /** Callback fired when the user selects a different option. */
  onChange: (value: string) => void;
  /** Placeholder text shown when no value is selected. */
  placeholder?: string;
  /** Optional label displayed above the dropdown trigger. */
  label?: string;
}

/**
 * Renders a custom styled dropdown with keyboard navigation and animation.
 *
 * @returns A `<div>` containing the trigger button and dropdown listbox.
 */
export function Select({
  options,
  value,
  onChange,
  placeholder = "Select…",
  label,
}: SelectProps): React.JSX.Element {
  const [isOpen, setIsOpen] = useState(false);
  const [highlightedIndex, setHighlightedIndex] = useState(-1);
  const containerRef = useRef<HTMLDivElement>(null);
  const triggerRef = useRef<HTMLButtonElement>(null);
  const listboxRef = useRef<HTMLUListElement>(null);

  const selectedOption = options.find((opt) => opt.value === value);

  /* ── Open/close handlers ── */
  const openDropdown = useCallback((): void => {
    setIsOpen(true);
    const currentIndex = options.findIndex((opt) => opt.value === value);
    setHighlightedIndex(currentIndex >= 0 ? currentIndex : 0);
  }, [options, value]);

  const closeDropdown = useCallback((): void => {
    setIsOpen(false);
    setHighlightedIndex(-1);
    triggerRef.current?.focus();
  }, []);

  const selectOption = useCallback(
    (optionValue: string): void => {
      onChange(optionValue);
      closeDropdown();
    },
    [onChange, closeDropdown],
  );

  /* ── Click-outside handler ── */
  useEffect(() => {
    if (!isOpen) return;

    function handleClickOutside(event: MouseEvent): void {
      if (
        containerRef.current &&
        !containerRef.current.contains(event.target as Node)
      ) {
        closeDropdown();
      }
    }

    document.addEventListener("mousedown", handleClickOutside);
    return () => document.removeEventListener("mousedown", handleClickOutside);
  }, [isOpen, closeDropdown]);

  /* ── Keyboard navigation ── */
  const handleKeyDown = useCallback(
    (event: React.KeyboardEvent): void => {
      if (!isOpen) {
        if (
          event.key === "ArrowDown" ||
          event.key === "ArrowUp" ||
          event.key === "Enter" ||
          event.key === " "
        ) {
          event.preventDefault();
          openDropdown();
        }
        return;
      }

      switch (event.key) {
        case "ArrowDown": {
          event.preventDefault();
          setHighlightedIndex((prev) =>
            prev < options.length - 1 ? prev + 1 : 0,
          );
          break;
        }
        case "ArrowUp": {
          event.preventDefault();
          setHighlightedIndex((prev) =>
            prev > 0 ? prev - 1 : options.length - 1,
          );
          break;
        }
        case "Enter":
        case " ": {
          event.preventDefault();
          if (highlightedIndex >= 0 && highlightedIndex < options.length) {
            selectOption(options[highlightedIndex].value);
          }
          break;
        }
        case "Escape": {
          event.preventDefault();
          closeDropdown();
          break;
        }
        case "Tab": {
          closeDropdown();
          break;
        }
      }
    },
    [isOpen, highlightedIndex, options, openDropdown, closeDropdown, selectOption],
  );

  /* ── Scroll highlighted option into view ── */
  useEffect(() => {
    if (!isOpen || highlightedIndex < 0) return;
    const listbox = listboxRef.current;
    if (!listbox) return;
    const highlighted = listbox.children[highlightedIndex] as HTMLElement | undefined;
    highlighted?.scrollIntoView({ block: "nearest" });
  }, [isOpen, highlightedIndex]);

  const selectId = label
    ? `select-${label.toLowerCase().replace(/\s+/g, "-")}`
    : "select";

  return (
    <div ref={containerRef} className="relative" data-testid="select-wrapper">
      {/* ── Optional label ── */}
      {label && (
        <label
          htmlFor={selectId}
          className="block text-s font-medium text-ds-text-muted mb-xs"
        >
          {label}
        </label>
      )}

      {/* ── Trigger button ── */}
      <button
        ref={triggerRef}
        id={selectId}
        type="button"
        role="combobox"
        aria-expanded={isOpen}
        aria-haspopup="listbox"
        aria-activedescendant={
          isOpen && highlightedIndex >= 0
            ? `${selectId}-option-${String(highlightedIndex)}`
            : undefined
        }
        className="w-full flex items-center justify-between gap-sm
                   py-sm px-md bg-ds-bg border border-ds-border rounded-md
                   text-ds-text text-md font-sans cursor-pointer
                   transition-[border-color] duration-150
                   focus:outline-none focus:border-ds-primary
                   focus:shadow-[0_0_0_3px_var(--color-primary-muted)]"
        onClick={() => (isOpen ? closeDropdown() : openDropdown())}
        onKeyDown={handleKeyDown}
        data-testid="select-trigger"
      >
        <span className={selectedOption ? "text-ds-text" : "text-ds-text-muted"}>
          {selectedOption ? (
            <span className="inline-flex items-center gap-sm">
              {selectedOption.icon}
              {selectedOption.label}
            </span>
          ) : (
            placeholder
          )}
        </span>
        {/* Chevron icon */}
        <svg
          className={`w-4 h-4 text-ds-text-muted transition-transform duration-150
                      ${isOpen ? "rotate-180" : ""}`}
          xmlns="http://www.w3.org/2000/svg"
          viewBox="0 0 20 20"
          fill="currentColor"
          aria-hidden="true"
        >
          <path
            fillRule="evenodd"
            d="M5.23 7.21a.75.75 0 011.06.02L10 11.168l3.71-3.938a.75.75 0
               111.08 1.04l-4.25 4.5a.75.75 0 01-1.08 0l-4.25-4.5a.75.75
               0 01.02-1.06z"
            clipRule="evenodd"
          />
        </svg>
      </button>

      {/* ── Dropdown listbox ── */}
      {isOpen && (
        <ul
          ref={listboxRef}
          role="listbox"
          aria-label={label ?? "Options"}
          className="absolute z-50 w-full mt-xs bg-ds-surface border border-ds-border
                     rounded-md shadow-ds-lg max-h-60 overflow-y-auto
                     animate-[dropdown-appear_150ms_ease-out]"
          data-testid="select-listbox"
        >
          {options.map((option, index) => {
            const isSelected = option.value === value;
            const isHighlighted = index === highlightedIndex;

            return (
              <li
                key={option.value}
                id={`${selectId}-option-${String(index)}`}
                role="option"
                aria-selected={isSelected}
                className={`flex items-center gap-sm py-sm px-md cursor-pointer
                            text-md transition-colors duration-100
                            ${isHighlighted ? "bg-ds-surface-hover text-ds-text" : "text-ds-text"}
                            ${isSelected ? "font-semibold" : "font-normal"}`}
                onClick={() => selectOption(option.value)}
                onMouseEnter={() => setHighlightedIndex(index)}
                data-testid={`select-option-${option.value}`}
              >
                {option.icon && (
                  <span className="shrink-0" aria-hidden="true">
                    {option.icon}
                  </span>
                )}
                {option.label}
                {isSelected && (
                  <svg
                    className="w-4 h-4 ml-auto text-ds-primary shrink-0"
                    xmlns="http://www.w3.org/2000/svg"
                    viewBox="0 0 20 20"
                    fill="currentColor"
                    aria-hidden="true"
                  >
                    <path
                      fillRule="evenodd"
                      d="M16.704 4.153a.75.75 0 01.143 1.052l-8 10.5a.75.75
                         0 01-1.127.075l-4.5-4.5a.75.75 0 011.06-1.06l3.894
                         3.893 7.48-9.817a.75.75 0 011.05-.143z"
                      clipRule="evenodd"
                    />
                  </svg>
                )}
              </li>
            );
          })}
        </ul>
      )}
    </div>
  );
}
