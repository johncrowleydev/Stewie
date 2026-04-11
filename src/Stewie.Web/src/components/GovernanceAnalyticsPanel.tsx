/**
 * GovernanceAnalyticsPanel — Dashboard panel showing governance failure analytics.
 * Displays pass rate, top failing rules with trend arrows, and GOV update suggestions.
 *
 * Features:
 * - Summary stat cards (total jobs, governance runs, pass rate)
 * - Visual pass rate bar with color-coded thresholds
 * - Top failing rules table with trend indicators (↑ ↓ →)
 * - GOV update suggestion cards
 * - Time filter selector (7d, 30d, 90d)
 *
 * REF: JOB-011 T-106, CON-002 v1.8.0
 */
import { useState, useEffect, useCallback } from "react";
import { fetchGovernanceAnalytics } from "../api/client";
import { IconBarChart } from "./Icons";
import type { GovernanceAnalytics, FailingRule } from "../types";

/** Time filter options */
const TIME_FILTERS = [
  { label: "7d", value: 7 },
  { label: "30d", value: 30 },
  { label: "90d", value: 90 },
] as const;

/** Trend arrow mapping */
const TREND_ARROWS: Record<FailingRule["trend"], string> = {
  increasing: "↑",
  decreasing: "↓",
  stable: "→",
};

/**
 * Determines pass rate color class based on threshold.
 */
function passRateClass(rate: number): string {
  if (rate >= 0.85) return "good";
  if (rate >= 0.60) return "moderate";
  return "poor";
}

interface GovernanceAnalyticsPanelProps {
  /** Optional project ID to filter analytics */
  projectId?: string;
}

/**
 * GovernanceAnalyticsPanel — fetches and renders governance failure analytics.
 */
export function GovernanceAnalyticsPanel({ projectId }: GovernanceAnalyticsPanelProps) {
  const [days, setDays] = useState(30);
  const [data, setData] = useState<GovernanceAnalytics | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const loadAnalytics = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const analytics = await fetchGovernanceAnalytics(days, projectId);
      setData(analytics);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to load analytics");
      setData(null);
    } finally {
      setLoading(false);
    }
  }, [days, projectId]);

  useEffect(() => {
    void loadAnalytics();
  }, [loadAnalytics]);

  if (loading) {
    return (
      <div className="analytics-panel" id="analytics-panel">
        <div className="analytics-header">
          <h2>Governance Analytics</h2>
        </div>
        <div className="skeleton skeleton-card" style={{ marginBottom: 16 }} />
        <div className="skeleton skeleton-card" />
      </div>
    );
  }

  if (error) {
    return (
      <div className="analytics-panel" id="analytics-panel">
        <div className="analytics-header">
          <h2>Governance Analytics</h2>
        </div>
        <div className="error-state">
          <h3>Analytics Unavailable</h3>
          <p>{error}</p>
        </div>
      </div>
    );
  }

  if (!data || data.totalGovernanceRuns === 0) {
    return (
      <div className="analytics-panel" id="analytics-panel">
        <div className="analytics-header">
          <h2>Governance Analytics</h2>
          <TimeFilter days={days} onChange={setDays} />
        </div>
        <div className="analytics-empty">
          <div className="empty-icon">--</div>
          <h3>No governance data</h3>
          <p>Run some jobs with governance checks to see analytics here.</p>
        </div>
      </div>
    );
  }

  const passPercent = Math.round(data.passRate * 100);
  const rateClass = passRateClass(data.passRate);

  return (
    <div className="analytics-panel" id="analytics-panel">
      {/* Header with time filter */}
      <div className="analytics-header">
        <h2>Governance Analytics</h2>
        <TimeFilter days={days} onChange={setDays} />
      </div>

      {/* Summary stat cards */}
      <div className="analytics-summary" id="analytics-summary">
        <div className="analytics-stat">
          <div className="analytics-stat-value blue">{data.totalJobs}</div>
          <div className="analytics-stat-label">Total Jobs</div>
        </div>
        <div className="analytics-stat">
          <div className="analytics-stat-value">{data.totalGovernanceRuns}</div>
          <div className="analytics-stat-label">Governance Runs</div>
        </div>
        <div className="analytics-stat">
          <div className={`analytics-stat-value ${rateClass === "poor" ? "red" : "green"}`}>
            {passPercent}%
          </div>
          <div className="analytics-stat-label">Pass Rate</div>
          <div className="pass-rate-bar">
            <div
              className={`pass-rate-fill ${rateClass}`}
              style={{ width: `${passPercent}%` }}
            />
          </div>
        </div>
      </div>

      {/* Top failing rules table */}
      {data.topFailingRules.length > 0 && (
        <div className="analytics-section" id="failing-rules-section">
          <h3><IconBarChart size={14} className="section-heading-icon" /> Top Failing Rules</h3>
          <table className="data-table">
            <thead>
              <tr>
                <th>Rule</th>
                <th>Name</th>
                <th>Failures</th>
                <th>Trend</th>
              </tr>
            </thead>
            <tbody>
              {data.topFailingRules.map((rule) => (
                <tr key={rule.ruleId}>
                  <td className="mono">{rule.ruleId}</td>
                  <td>{rule.ruleName}</td>
                  <td style={{ fontWeight: 700 }}>{rule.failCount}</td>
                  <td>
                    <span className={`trend-indicator ${rule.trend}`}>
                      {TREND_ARROWS[rule.trend]} {rule.trend}
                    </span>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {/* GOV update suggestions */}
      {data.suggestedGovUpdates.length > 0 && (
        <div className="analytics-section" id="gov-suggestions-section">
          <h3>Suggested Updates</h3>
          <div className="suggestion-list">
            {data.suggestedGovUpdates.map((suggestion, idx) => (
              <div className="suggestion-item" key={idx}>
                
                <div className="suggestion-content">
                  <div className="suggestion-doc">{suggestion.govDoc}</div>
                  <div className="suggestion-reason">{suggestion.reason}</div>
                </div>
              </div>
            ))}
          </div>
        </div>
      )}
    </div>
  );
}

/**
 * TimeFilter — toggle buttons for time period selection.
 */
function TimeFilter({
  days,
  onChange,
}: {
  days: number;
  onChange: (d: number) => void;
}) {
  return (
    <div className="analytics-time-filter" id="analytics-time-filter">
      {TIME_FILTERS.map((f) => (
        <button
          key={f.value}
          className={`time-filter-btn ${days === f.value ? "active" : ""}`}
          onClick={() => onChange(f.value)}
        >
          {f.label}
        </button>
      ))}
    </div>
  );
}
