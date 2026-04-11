"""
stewie_api_client.py — HTTP client for Stewie REST API.

Provides typed methods for interacting with the Stewie control plane
from within agent containers. All requests include Bearer token auth
read from the Docker secrets mount.

REF: JOB-022 T-191, CON-002
"""

import json
import logging
import os
from datetime import datetime, timezone
from typing import Any

# requests is installed in the Architect Dockerfile (T-198)
try:
    import requests
except ImportError:
    requests = None  # type: ignore[assignment]

logger = logging.getLogger(__name__)


class StewieApiError(Exception):
    """Raised when the Stewie API returns an error response."""

    def __init__(self, status_code: int, message: str, response_body: str = ""):
        self.status_code = status_code
        self.response_body = response_body
        super().__init__(f"Stewie API error {status_code}: {message}")


class StewieApiClient:
    """
    Lightweight HTTP client for Stewie REST API.

    Reads the agent token from /run/secrets/agent_token on init.
    All requests include the Authorization: Bearer header.

    Args:
        base_url: Stewie API base URL (e.g. http://stewie-api:5275).
        agent_token: Bearer token for authentication. If None, reads from secrets file.
        timeout: Request timeout in seconds.
    """

    def __init__(
        self,
        base_url: str,
        agent_token: str | None = None,
        timeout: int = 30,
    ):
        if requests is None:
            raise ImportError("requests library is required. Install with: pip install requests")

        self.base_url = base_url.rstrip("/")
        self.timeout = timeout
        self.token = agent_token or self._read_token()
        self._session = requests.Session()
        self._session.headers.update({
            "Content-Type": "application/json",
            "Authorization": f"Bearer {self.token}",
        })
        logger.info("StewieApiClient initialized: base_url=%s", self.base_url)

    @staticmethod
    def _read_token() -> str:
        """Read agent token from Docker secrets mount."""
        secrets_path = "/run/secrets/agent_token"
        if os.path.isfile(secrets_path):
            with open(secrets_path, "r") as f:
                token = f.read().strip()
                if token:
                    logger.info("Agent token loaded from %s", secrets_path)
                    return token

        env_token = os.environ.get("AGENT_TOKEN", "")
        if env_token:
            logger.info("Agent token loaded from AGENT_TOKEN env var")
            return env_token

        logger.warning("No agent token found — API calls may fail with 401")
        return ""

    def _request(self, method: str, path: str, **kwargs: Any) -> dict:
        """
        Make an HTTP request to the Stewie API.

        Args:
            method: HTTP method (GET, POST, PUT, DELETE).
            path: API path relative to base_url (e.g. /api/projects).
            **kwargs: Additional arguments passed to requests.

        Returns:
            Parsed JSON response body.

        Raises:
            StewieApiError: On 4xx/5xx responses.
        """
        url = f"{self.base_url}{path}"
        logger.debug("%s %s", method, url)

        try:
            response = self._session.request(
                method, url, timeout=self.timeout, **kwargs
            )
        except requests.ConnectionError as e:
            raise StewieApiError(0, f"Connection failed: {e}") from e
        except requests.Timeout as e:
            raise StewieApiError(0, f"Request timed out: {e}") from e

        if response.status_code >= 400:
            body = response.text[:500]
            logger.error(
                "API error: %s %s → %d: %s",
                method, url, response.status_code, body,
            )
            raise StewieApiError(response.status_code, body, response.text)

        if not response.content:
            return {}

        try:
            return response.json()
        except json.JSONDecodeError:
            return {"raw": response.text}

    # ── Project ─────────────────────────────────────────────────────────

    def get_project(self, project_id: str) -> dict:
        """
        Get project details by ID.

        Args:
            project_id: Project UUID.

        Returns:
            Project object with id, name, repoUrl, etc.
        """
        return self._request("GET", f"/api/projects/{project_id}")

    # ── Jobs ────────────────────────────────────────────────────────────

    def create_job(self, project_id: str, title: str, description: str) -> dict:
        """
        Create a new job for a project.

        Args:
            project_id: Project UUID.
            title: Job title / objective.
            description: Detailed job description / scope.

        Returns:
            Created job object with id, status, tasks, etc.
        """
        return self._request("POST", "/api/jobs", json={
            "projectId": project_id,
            "objective": title,
            "scope": description,
        })

    def create_multi_task_job(
        self, project_id: str, tasks: list[dict]
    ) -> dict:
        """
        Create a multi-task DAG job.

        Args:
            project_id: Project UUID.
            tasks: List of task definitions with clientId, objective, dependsOn, etc.

        Returns:
            Created job object with task list.
        """
        return self._request("POST", "/api/jobs", json={
            "projectId": project_id,
            "tasks": tasks,
        })

    def get_jobs(self, project_id: str) -> list:
        """
        List all jobs for a project.

        Args:
            project_id: Project UUID.

        Returns:
            List of job objects.
        """
        result = self._request("GET", f"/api/jobs?projectId={project_id}")
        if isinstance(result, list):
            return result
        return result.get("jobs", result) if isinstance(result, dict) else []

    def get_job(self, job_id: str) -> dict:
        """
        Get a job by ID with its tasks.

        Args:
            job_id: Job UUID.

        Returns:
            Job object with nested tasks.
        """
        return self._request("GET", f"/api/jobs/{job_id}")

    # ── Tasks ───────────────────────────────────────────────────────────

    def create_task(
        self, job_id: str, title: str, description: str, role: str = "developer"
    ) -> dict:
        """
        Create a task within an existing job.

        Note: Tasks are typically created as part of job creation (multi-task mode).
        This method exists for when individual task creation is needed.

        Args:
            job_id: Parent job UUID.
            title: Task title / objective.
            description: Task description / scope.
            role: Agent role (developer, tester).

        Returns:
            Created task object.
        """
        return self._request("POST", f"/api/jobs/{job_id}/tasks", json={
            "objective": title,
            "scope": description,
            "role": role,
        })

    def update_task_status(self, task_id: str, status: str) -> dict:
        """
        Update a task's status.

        Args:
            task_id: Task UUID.
            status: New status (Pending, Running, Completed, Failed).

        Returns:
            Updated task object.
        """
        return self._request("PUT", f"/api/tasks/{task_id}", json={
            "status": status,
        })

    # ── Chat ────────────────────────────────────────────────────────────

    def get_chat_history(self, project_id: str, limit: int = 50) -> list:
        """
        Get recent chat messages for a project.

        Args:
            project_id: Project UUID.
            limit: Maximum number of messages to return.

        Returns:
            List of chat message objects (oldest first).
        """
        result = self._request(
            "GET", f"/api/projects/{project_id}/chat?limit={limit}"
        )
        if isinstance(result, dict):
            return result.get("messages", [])
        return result if isinstance(result, list) else []

    def send_chat_message(
        self,
        project_id: str,
        content: str,
        sender_role: str = "Architect",
    ) -> dict:
        """
        Send a chat message to a project.

        Args:
            project_id: Project UUID.
            content: Message text (markdown supported).
            sender_role: Who is sending (Architect, System).

        Returns:
            Created message object.
        """
        return self._request(
            "POST",
            f"/api/projects/{project_id}/chat",
            json={"content": content},
        )

    # ── Agents ──────────────────────────────────────────────────────────

    def launch_agent(
        self,
        project_id: str,
        role: str,
        runtime: str,
        task_id: str | None = None,
    ) -> dict:
        """
        Launch a new agent container for a project.

        Args:
            project_id: Project UUID.
            role: Agent role (developer, tester).
            runtime: Runtime name (stub, opencode).
            task_id: Optional task UUID to assign to the agent.

        Returns:
            Agent session object with id, containerId, status.
        """
        payload: dict[str, Any] = {
            "projectId": project_id,
            "agentRole": role,
            "runtimeName": runtime,
        }
        if task_id:
            payload["taskId"] = task_id

        return self._request("POST", "/api/agents/launch", json=payload)

    def get_agent_sessions(self, project_id: str) -> list:
        """
        List all agent sessions for a project.

        Args:
            project_id: Project UUID.

        Returns:
            List of agent session objects.
        """
        result = self._request("GET", f"/api/projects/{project_id}/agents")
        if isinstance(result, dict):
            return result.get("sessions", [])
        return result if isinstance(result, list) else []
