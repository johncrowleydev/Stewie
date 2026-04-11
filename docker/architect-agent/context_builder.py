"""
context_builder.py — Assembles LLM context from project state and chat history.

Combines project metadata, recent chat history, active jobs, and the human's
current message into a structured prompt for the Architect Agent LLM.

REF: JOB-022 T-193, AGT-001
"""

import logging
from typing import Any

logger = logging.getLogger(__name__)


class ContextBuilder:
    """
    Builds complete LLM prompts from Stewie project state.

    Fetches project info, chat history, and active jobs from the API,
    then formats them into a structured prompt with token budget management.

    Args:
        api_client: StewieApiClient instance for API calls.
    """

    # System prompt instructing the LLM on its Architect role
    SYSTEM_PROMPT = """You are the Architect Agent for the Stewie project management system.
Given the project context and human request, produce a structured job plan.
Output your plan as a JSON object with this schema:
{
  "summary": "human-readable plan description",
  "jobs": [{
    "title": "...",
    "tasks": [{
      "title": "...",
      "description": "...",
      "role": "developer | tester",
      "dependsOn": []
    }]
  }]
}
Include a plain-text explanation before the JSON block.
Be concise, specific, and actionable. Each task should be independently executable."""

    def __init__(self, api_client: Any):
        self._api = api_client

    def build_context(self, project_id: str, human_message: str) -> str:
        """
        Build a complete LLM prompt with project context.

        Fetches and assembles:
        1. Project metadata (name, repo URL)
        2. Chat history (last 20 messages)
        3. Active jobs and their status
        4. The human's current message
        5. System instructions

        Args:
            project_id: Project UUID.
            human_message: The human's current chat message.

        Returns:
            Formatted prompt string ready for LLM invocation.
        """
        sections: list[str] = []

        # 1. Project metadata
        project_section = self._build_project_section(project_id)
        if project_section:
            sections.append(project_section)

        # 2. Chat history
        chat_section = self._build_chat_section(project_id)
        if chat_section:
            sections.append(chat_section)

        # 3. Active jobs
        jobs_section = self._build_jobs_section(project_id)
        if jobs_section:
            sections.append(jobs_section)

        # 4. Human's request
        sections.append(f"## Human's Request\n\n{human_message}")

        # 5. System instructions
        sections.append(f"## Your Instructions\n\n{self.SYSTEM_PROMPT}")

        full_context = "\n\n---\n\n".join(sections)

        # Token budget enforcement
        full_context = self.truncate_to_budget(full_context)

        token_estimate = self.estimate_tokens(full_context)
        logger.info(
            "Built context for project %s: ~%d tokens",
            project_id, token_estimate,
        )

        return full_context

    def _build_project_section(self, project_id: str) -> str:
        """Fetch project metadata and format as context section."""
        try:
            project = self._api.get_project(project_id)
            name = project.get("name", "Unknown")
            repo_url = project.get("repoUrl", "N/A")
            return f"## Project: {name}\n\nRepository: {repo_url}"
        except Exception as e:
            logger.warning("Failed to fetch project %s: %s", project_id, e)
            return f"## Project: {project_id}\n\n(Could not fetch project details)"

    def _build_chat_section(self, project_id: str) -> str:
        """Fetch recent chat history and format as context section."""
        try:
            messages = self._api.get_chat_history(project_id, limit=20)
            if not messages:
                return ""

            lines = ["## Recent Conversation\n"]
            for msg in messages:
                role = msg.get("senderRole", "?")
                name = msg.get("senderName", "?")
                content = msg.get("content", "")
                # Truncate individual messages to keep context manageable
                if len(content) > 500:
                    content = content[:497] + "..."
                lines.append(f"**{role}** ({name}): {content}")

            return "\n\n".join(lines)
        except Exception as e:
            logger.warning("Failed to fetch chat history for %s: %s", project_id, e)
            return ""

    def _build_jobs_section(self, project_id: str) -> str:
        """Fetch active jobs and format as context section."""
        try:
            jobs = self._api.get_jobs(project_id)
            if not jobs:
                return ""

            # Filter to non-terminal jobs
            active_jobs = [
                j for j in jobs
                if j.get("status") in ("Pending", "Running")
            ]
            if not active_jobs:
                return ""

            lines = ["## Active Jobs\n"]
            for job in active_jobs[:10]:  # Limit to 10 most relevant
                job_id = job.get("id", "?")
                status = job.get("status", "?")
                task_count = job.get("taskCount", 0)
                completed = job.get("completedTaskCount", 0)
                lines.append(
                    f"- Job `{job_id}`: {status} "
                    f"({completed}/{task_count} tasks complete)"
                )

            return "\n".join(lines)
        except Exception as e:
            logger.warning("Failed to fetch jobs for %s: %s", project_id, e)
            return ""

    @staticmethod
    def estimate_tokens(text: str) -> int:
        """
        Rough token estimate using chars / 4 heuristic.

        This is intentionally simple — within 2x of actual for English text.
        Good enough for budget enforcement without a tokenizer dependency.

        Args:
            text: Input text to estimate.

        Returns:
            Approximate token count.
        """
        return len(text) // 4

    @staticmethod
    def truncate_to_budget(text: str, max_tokens: int = 100_000) -> str:
        """
        Truncate oldest context to fit within token budget.

        Preserves the most recent content (end of string) since that contains
        the human's message and system instructions. Truncation removes from
        the beginning (oldest chat history, etc.).

        Args:
            text: Full context string.
            max_tokens: Maximum token budget.

        Returns:
            Truncated text that fits within budget.
        """
        estimated = len(text) // 4
        if estimated <= max_tokens:
            return text

        # Calculate char budget (4 chars ≈ 1 token)
        char_budget = max_tokens * 4
        if len(text) <= char_budget:
            return text

        truncated = text[len(text) - char_budget:]
        # Try to truncate at a clean line boundary
        newline_idx = truncated.find("\n")
        if newline_idx > 0 and newline_idx < 200:
            truncated = truncated[newline_idx + 1:]

        return f"[Context truncated to fit token budget]\n\n{truncated}"
