/**
 * GovernanceReportPanel — Displays governance check results with visual grouping.
 * REF: CON-002 §4.6, JOB-008 T-077, JOB-027 T-407
 */
import { useState } from "react";
import { skeleton } from "../tw";
import type { GovernanceReport, GovernanceCheckResult } from "../types";
import { IconCheck, IconX, IconChevronRight, IconChevronDown } from "./Icons";

interface GovernanceReportPanelProps {
  report: GovernanceReport | null;
  loading?: boolean;
  error?: string | null;
}

function groupByCategory(checks: GovernanceCheckResult[]): Record<string, GovernanceCheckResult[]> {
  const groups: Record<string, GovernanceCheckResult[]> = {};
  for (const check of checks) { const cat = check.category || "Other"; if (!groups[cat]) groups[cat] = []; groups[cat].push(check); }
  return groups;
}

const CATEGORY_LABELS: Record<string, string> = {
  "GOV-001": "Documentation Standards", "GOV-002": "Testing Protocol", "GOV-003": "Coding Standards",
  "GOV-004": "Error Handling", "GOV-005": "Development Lifecycle", "GOV-006": "Logging Specification",
  "GOV-008": "Infrastructure & Operations", "SEC-001": "Secret Scanning",
};

export function GovernanceReportPanel({ report, loading, error }: GovernanceReportPanelProps) {
  const [expandedRules, setExpandedRules] = useState<Set<string>>(new Set());

  function toggleRule(ruleId: string) {
    setExpandedRules(prev => { const next = new Set(prev); next.has(ruleId) ? next.delete(ruleId) : next.add(ruleId); return next; });
  }

  if (loading) {
    return (
      <div className="bg-ds-surface border border-ds-border rounded-md p-md" id="governance-panel">
        <div className={`${skeleton} h-8 mb-md`} />
        <div className={`${skeleton} h-5 mb-sm`} />
        {[1, 2, 3].map((i) => <div key={i} className={`${skeleton} h-10 mb-sm`} />)}
      </div>
    );
  }

  if (error) {
    return (
      <div className="bg-[rgba(229,72,77,0.06)] border border-[rgba(229,72,77,0.2)] rounded-md p-md" id="governance-panel">
        <div className="flex items-center gap-sm text-ds-failed text-s">
          <span className="w-5 h-5 rounded-full bg-ds-failed text-white flex items-center justify-center text-xs font-bold">!</span>
          <span>{error}</span>
        </div>
      </div>
    );
  }

  if (!report) {
    return (
      <div className="bg-ds-surface border border-ds-border rounded-md p-md text-center text-ds-text-muted text-s italic" id="governance-panel">
        <span className="opacity-30 mr-sm">--</span> No governance report available
      </div>
    );
  }

  const passRate = report.totalChecks > 0 ? Math.round((report.passedChecks / report.totalChecks) * 100) : 100;
  const grouped = groupByCategory(report.checks);
  const categoryKeys = Object.keys(grouped).sort();

  return (
    <div className="bg-ds-surface border border-ds-border rounded-md p-md" id="governance-panel">
      {/* Verdict */}
      <div className="flex items-center justify-between mb-md">
        <div className={`inline-flex items-center gap-sm py-xs px-md rounded-md font-bold text-s uppercase tracking-wider ${report.passed ? "bg-[rgba(111,172,80,0.15)] text-ds-completed" : "bg-[rgba(229,72,77,0.15)] text-ds-failed"}`}>
          <span>{report.passed ? "PASS" : "FAIL"}</span>
          <span className="font-normal text-xs">{report.passed ? "PASSED" : "FAILED"}</span>
        </div>
        <span className="text-s text-ds-text-muted">{report.passedChecks}/{report.totalChecks} checks passed</span>
      </div>

      {/* Progress bar */}
      <div className="h-1.5 bg-ds-border rounded-full overflow-hidden mb-md" id="governance-progress">
        <div className="h-full rounded-full transition-all duration-300" style={{ width: `${passRate}%`, background: report.passed ? "var(--color-completed)" : "var(--color-failed)" }} />
      </div>

      {/* Categories */}
      {categoryKeys.map((category) => {
        const checks = grouped[category];
        const catPassed = checks.filter(c => c.passed).length;
        const allPassed = catPassed === checks.length;
        return (
          <div className="mb-md last:mb-0" key={category} id={`gov-cat-${category}`}>
            <div className="flex items-center gap-sm mb-sm">
              <span className={`text-s font-bold ${allPassed ? "text-ds-completed" : "text-ds-failed"}`}>{allPassed ? <IconCheck size={14} /> : <IconX size={14} />}</span>
              <span className="text-s font-semibold text-ds-text">{CATEGORY_LABELS[category] || category}</span>
              <span className="text-xs text-ds-text-muted font-mono">{category}</span>
              <span className="ml-auto text-xs text-ds-text-muted">{catPassed}/{checks.length}</span>
            </div>
            <div className="flex flex-col gap-xs ml-md">
              {checks.map((check) => (
                <div key={check.ruleId} id={`gov-rule-${check.ruleId}`}>
                  <div
                    className={`flex items-center gap-sm py-xs px-sm rounded-sm text-s transition-colors duration-100 ${!check.passed && check.details ? "cursor-pointer hover:bg-ds-surface-hover" : ""}`}
                    onClick={() => { if (!check.passed && check.details) toggleRule(check.ruleId); }}
                    role={!check.passed && check.details ? "button" : undefined}
                    tabIndex={!check.passed && check.details ? 0 : undefined}
                  >
                    <span className={`text-xs font-bold ${check.passed ? "text-ds-completed" : "text-ds-failed"}`}>{check.passed ? <IconCheck size={12} /> : <IconX size={12} />}</span>
                    <span className="text-ds-text flex-1">{check.ruleName}</span>
                    <span className="font-mono text-xs text-ds-text-muted">{check.ruleId}</span>
                    <span className={`text-[10px] py-px px-1.5 rounded-full font-medium ${check.severity === "error" ? "bg-[rgba(229,72,77,0.12)] text-ds-failed" : "bg-[rgba(245,166,35,0.12)] text-ds-warning"}`}>{check.severity}</span>
                    {!check.passed && check.details && (
                      <span className="text-xs text-ds-text-muted">{expandedRules.has(check.ruleId) ? <IconChevronDown size={12} /> : <IconChevronRight size={12} />}</span>
                    )}
                  </div>
                  {!check.passed && check.details && expandedRules.has(check.ruleId) && (
                    <div className="ml-6 mt-xs animate-[slideDown_0.2s_ease-out]">
                      <pre className="overflow-x-auto p-sm bg-ds-bg rounded-sm font-mono text-xs text-ds-text-muted leading-relaxed max-h-[200px] overflow-y-auto">{check.details}</pre>
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
