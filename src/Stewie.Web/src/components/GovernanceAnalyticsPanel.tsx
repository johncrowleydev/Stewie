/**
 * GovernanceAnalyticsPanel — Dashboard panel showing governance failure analytics.
 * REF: JOB-011 T-106, CON-002 v1.8.0, JOB-027 T-407
 */
import { useState, useEffect, useCallback } from "react";
import { fetchGovernanceAnalytics } from "../api/client";
import { IconBarChart } from "./Icons";
import { card, skeleton, sectionHeading, dataTable, th, td } from "../tw";
import type { GovernanceAnalytics, FailingRule } from "../types";

const TIME_FILTERS = [{ label: "7d", value: 7 }, { label: "30d", value: 30 }, { label: "90d", value: 90 }] as const;
const TREND_ARROWS: Record<FailingRule["trend"], string> = { increasing: "↑", decreasing: "↓", stable: "→" };
const TREND_COLORS: Record<FailingRule["trend"], string> = { increasing: "text-ds-failed", decreasing: "text-ds-completed", stable: "text-ds-text-muted" };

function passRateColor(rate: number): string {
  if (rate >= 0.85) return "text-ds-completed";
  if (rate >= 0.60) return "text-ds-warning";
  return "text-ds-failed";
}

function passRateBarColor(rate: number): string {
  if (rate >= 0.85) return "bg-ds-completed";
  if (rate >= 0.60) return "bg-ds-warning";
  return "bg-ds-failed";
}

function TimeFilter({ days, onChange }: { days: number; onChange: (d: number) => void }) {
  return (
    <div className="flex gap-xs" id="analytics-time-filter">
      {TIME_FILTERS.map((f) => (
        <button
          key={f.value}
          className={`px-sm py-xs rounded-md text-xs font-medium cursor-pointer border transition-all duration-150 ${days === f.value ? "bg-ds-primary text-white border-ds-primary dark:bg-transparent dark:text-ds-primary dark:hover:bg-[rgba(111,172,80,0.15)]" : "bg-transparent text-ds-text-muted border-ds-border hover:text-ds-text hover:bg-ds-surface-hover"}`}
          onClick={() => onChange(f.value)}
        >
          {f.label}
        </button>
      ))}
    </div>
  );
}

interface GovernanceAnalyticsPanelProps { projectId?: string; }

export function GovernanceAnalyticsPanel({ projectId }: GovernanceAnalyticsPanelProps) {
  const [days, setDays] = useState(30);
  const [data, setData] = useState<GovernanceAnalytics | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const loadAnalytics = useCallback(async () => {
    setLoading(true); setError(null);
    try { setData(await fetchGovernanceAnalytics(days, projectId)); }
    catch (err) { setError(err instanceof Error ? err.message : "Failed to load analytics"); setData(null); }
    finally { setLoading(false); }
  }, [days, projectId]);

  useEffect(() => { void loadAnalytics(); }, [loadAnalytics]);

  if (loading) {
    return (
      <div className={card} id="analytics-panel">
        <h2 className={sectionHeading}>Governance Analytics</h2>
        <div className={`${skeleton} h-[80px] mb-md`} />
        <div className={`${skeleton} h-[120px]`} />
      </div>
    );
  }

  if (error) {
    return (
      <div className={card} id="analytics-panel">
        <h2 className={sectionHeading}>Governance Analytics</h2>
        <div className="bg-[rgba(229,72,77,0.08)] border border-[rgba(229,72,77,0.2)] rounded-lg p-lg text-center text-ds-failed">
          <h3 className="text-base font-semibold mb-sm">Analytics Unavailable</h3>
          <p className="text-md">{error}</p>
        </div>
      </div>
    );
  }

  if (!data || data.totalGovernanceRuns === 0) {
    return (
      <div className={card} id="analytics-panel">
        <div className="flex items-center justify-between mb-md">
          <h2 className={`${sectionHeading} mb-0`}>Governance Analytics</h2>
          <TimeFilter days={days} onChange={setDays} />
        </div>
        <div className="text-center p-2xl text-ds-text-muted">
          <div className="text-[3rem] mb-md opacity-30">--</div>
          <h3 className="text-base font-semibold text-ds-text mb-sm">No governance data</h3>
          <p className="text-md">Run some jobs with governance checks to see analytics here.</p>
        </div>
      </div>
    );
  }

  const passPercent = Math.round(data.passRate * 100);

  return (
    <div className={card} id="analytics-panel">
      <div className="flex items-center justify-between mb-md">
        <h2 className={`${sectionHeading} mb-0`}>Governance Analytics</h2>
        <TimeFilter days={days} onChange={setDays} />
      </div>

      {/* Summary stats */}
      <div className="grid grid-cols-3 gap-md mb-lg" id="analytics-summary">
        <div className="bg-ds-bg rounded-md p-md text-center">
          <div className="text-2xl font-bold text-ds-running">{data.totalJobs}</div>
          <div className="text-xs text-ds-text-muted mt-xs">Total Jobs</div>
        </div>
        <div className="bg-ds-bg rounded-md p-md text-center">
          <div className="text-2xl font-bold text-ds-text">{data.totalGovernanceRuns}</div>
          <div className="text-xs text-ds-text-muted mt-xs">Governance Runs</div>
        </div>
        <div className="bg-ds-bg rounded-md p-md text-center">
          <div className={`text-2xl font-bold ${passRateColor(data.passRate)}`}>{passPercent}%</div>
          <div className="text-xs text-ds-text-muted mt-xs">Pass Rate</div>
          <div className="h-1 bg-ds-border rounded-full overflow-hidden mt-sm">
            <div className={`h-full rounded-full transition-all duration-300 ${passRateBarColor(data.passRate)}`} style={{ width: `${passPercent}%` }} />
          </div>
        </div>
      </div>

      {/* Top failing rules */}
      {data.topFailingRules.length > 0 && (
        <div className="mb-lg" id="failing-rules-section">
          <h3 className="text-s font-semibold text-ds-text flex items-center gap-sm mb-sm [&_svg]:w-3.5 [&_svg]:h-3.5 [&_svg]:opacity-60"><IconBarChart size={14} /> Top Failing Rules</h3>
          <table className={dataTable}>
            <thead><tr>{["Rule", "Name", "Failures", "Trend"].map(h => <th key={h} className={th}>{h}</th>)}</tr></thead>
            <tbody>
              {data.topFailingRules.map((rule) => (
                <tr key={rule.ruleId} className="border-b border-ds-border last:border-b-0">
                  <td className={`${td} font-mono`}>{rule.ruleId}</td>
                  <td className={td}>{rule.ruleName}</td>
                  <td className={`${td} font-bold`}>{rule.failCount}</td>
                  <td className={td}><span className={`${TREND_COLORS[rule.trend]} font-medium`}>{TREND_ARROWS[rule.trend]} {rule.trend}</span></td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {/* Suggestions */}
      {data.suggestedGovUpdates.length > 0 && (
        <div id="gov-suggestions-section">
          <h3 className="text-s font-semibold text-ds-text mb-sm">Suggested Updates</h3>
          <div className="flex flex-col gap-sm">
            {data.suggestedGovUpdates.map((suggestion, idx) => (
              <div className="bg-ds-bg rounded-md p-sm flex flex-col gap-xs border-l-2 border-ds-primary" key={idx}>
                <div className="font-mono text-xs text-ds-primary font-semibold">{suggestion.govDoc}</div>
                <div className="text-s text-ds-text-muted">{suggestion.reason}</div>
              </div>
            ))}
          </div>
        </div>
      )}
    </div>
  );
}
