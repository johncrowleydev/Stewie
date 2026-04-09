/**
 * GovernanceReportPanel — Displays a governance report with grouped check results.
 *
 * Features:
 * - Overall verdict badge (PASS ✅ / FAIL ❌)
 * - Pass rate progress bar (e.g., "14/16 checks passed")
 * - Checks grouped by GOV category (GOV-001, GOV-002, etc.)
 * - Each rule: rule name, pass/fail icon, severity badge (error/warning)
 * - Expandable details for failed rules (shows output in code block)
 * - Graceful handling of null/empty report
 *
 * REF: CON-002 §4.6, JOB-008 T-077
 */
import { useState } from "react";
import type { GovernanceReport, GovernanceCheckResult } from "../types";

/** Props for the GovernanceReportPanel */
interface GovernanceReportPanelProps {
  /** The governance report data, or null if not yet loaded */
  report: GovernanceReport | null;
  /** Whether the report is currently loading */
  loading?: boolean;
  /** Optional error message */
  error?: string | null;
}

/** Groups check results by their GOV category */
function groupByCategory(checks: GovernanceCheckResult[]): Record<string, GovernanceCheckResult[]> {
  const groups: Record<string, GovernanceCheckResult[]> = {};
  for (const check of checks) {
    const cat = check.category || "Other";
    if (!groups[cat]) groups[cat] = [];
    groups[cat].push(check);
  }
  return groups;
}

/** Human-readable labels for common GOV categories */
const CATEGORY_LABELS: Record<string, string> = {
  "GOV-001": "Documentation Standards",
  "GOV-002": "Testing Protocol",
  "GOV-003": "Coding Standards",
  "GOV-004": "Error Handling",
  "GOV-005": "Development Lifecycle",
  "GOV-006": "Logging Specification",
  "GOV-008": "Infrastructure & Operations",
  "SEC-001": "Secret Scanning",
};

/** GovernanceReportPanel — renders governance check results with visual grouping */
export function GovernanceReportPanel({ report, loading, error }: GovernanceReportPanelProps) {
  const [expandedRules, setExpandedRules] = useState<Set<string>>(new Set());

  /** Toggle expanded state for a rule's details */
  function toggleRule(ruleId: string) {
    setExpandedRules((prev) => {
      const next = new Set(prev);
      if (next.has(ruleId)) {
        next.delete(ruleId);
      } else {
        next.add(ruleId);
      }
      return next;
    });
  }

  if (loading) {
    return (
      <div className="governance-panel" id="governance-panel">
        <div className="skeleton skeleton-row" style={{ height: 32, marginBottom: 16 }} />
        <div className="skeleton skeleton-row" style={{ height: 20, marginBottom: 12 }} />
        {[1, 2, 3].map((i) => (
          <div key={i} className="skeleton skeleton-row" style={{ height: 40, marginBottom: 8 }} />
        ))}
      </div>
    );
  }

  if (error) {
    return (
      <div className="governance-panel governance-panel--error" id="governance-panel">
        <div className="governance-error">
          <span className="governance-error-icon">⚠️</span>
          <span>{error}</span>
        </div>
      </div>
    );
  }

  if (!report) {
    return (
      <div className="governance-panel governance-panel--empty" id="governance-panel">
        <div className="governance-empty">
          <span className="governance-empty-icon">📋</span>
          <span>No governance report available</span>
        </div>
      </div>
    );
  }

  const passRate = report.totalChecks > 0
    ? Math.round((report.passedChecks / report.totalChecks) * 100)
    : 100;
  const grouped = groupByCategory(report.checks);
  const categoryKeys = Object.keys(grouped).sort();

  return (
    <div className="governance-panel" id="governance-panel">
      {/* Verdict Header */}
      <div className="governance-header">
        <div className={`governance-verdict ${report.passed ? "governance-verdict--pass" : "governance-verdict--fail"}`}>
          <span className="governance-verdict-icon">
            {report.passed ? "✅" : "❌"}
          </span>
          <span className="governance-verdict-label">
            {report.passed ? "PASSED" : "FAILED"}
          </span>
        </div>
        <div className="governance-stats">
          <span className="governance-stats-text">
            {report.passedChecks}/{report.totalChecks} checks passed
          </span>
        </div>
      </div>

      {/* Pass Rate Bar */}
      <div className="governance-progress" id="governance-progress">
        <div
          className="governance-progress-bar"
          style={{
            width: `${passRate}%`,
            background: report.passed
              ? "var(--color-completed)"
              : "var(--color-failed)",
          }}
        />
      </div>

      {/* Grouped Check Results */}
      {categoryKeys.map((category) => {
        const checks = grouped[category];
        const catPassed = checks.filter((c) => c.passed).length;
        const catTotal = checks.length;
        const allPassed = catPassed === catTotal;

        return (
          <div className="governance-category" key={category} id={`gov-cat-${category}`}>
            <div className="governance-category-header">
              <span className={`governance-category-icon ${allPassed ? "pass" : "fail"}`}>
                {allPassed ? "✓" : "✗"}
              </span>
              <span className="governance-category-name">
                {CATEGORY_LABELS[category] || category}
              </span>
              <span className="governance-category-id">{category}</span>
              <span className="governance-category-count">
                {catPassed}/{catTotal}
              </span>
            </div>
            <div className="governance-rules">
              {checks.map((check) => (
                <div
                  className={`governance-rule ${check.passed ? "governance-rule--pass" : "governance-rule--fail"}`}
                  key={check.ruleId}
                  id={`gov-rule-${check.ruleId}`}
                >
                  <div
                    className="governance-rule-row"
                    onClick={() => { if (!check.passed && check.details) toggleRule(check.ruleId); }}
                    role={!check.passed && check.details ? "button" : undefined}
                    tabIndex={!check.passed && check.details ? 0 : undefined}
                  >
                    <span className="governance-rule-icon">
                      {check.passed ? "✓" : "✗"}
                    </span>
                    <span className="governance-rule-name">{check.ruleName}</span>
                    <span className="governance-rule-id mono">{check.ruleId}</span>
                    <span className={`governance-severity governance-severity--${check.severity}`}>
                      {check.severity}
                    </span>
                    {!check.passed && check.details && (
                      <span className="governance-rule-expand">
                        {expandedRules.has(check.ruleId) ? "▼" : "▶"}
                      </span>
                    )}
                  </div>
                  {!check.passed && check.details && expandedRules.has(check.ruleId) && (
                    <div className="governance-rule-details">
                      <pre>{check.details}</pre>
                    </div>
                  )}
                </div>
              ))}
            </div>
          </div>
        );
      })}
    </div>
  );
}
