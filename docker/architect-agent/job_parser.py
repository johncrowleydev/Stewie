"""
job_parser.py — Parses structured LLM plan output into Stewie API calls.

Validates plan structure, enforces guardrails (max tasks/jobs),
and creates jobs + tasks via the Stewie REST API.

REF: JOB-022 T-195, CON-002
"""

import logging
import uuid
from typing import Any

logger = logging.getLogger(__name__)


class PlanValidationError(Exception):
    """Raised when a plan fails structural validation."""

    def __init__(self, errors: list[str]):
        self.errors = errors
        super().__init__(f"Plan validation failed: {'; '.join(errors)}")


class JobParser:
    """
    Parses LLM-generated plan JSON and creates jobs/tasks via the API.

    Validates plan structure before making any API calls. Enforces
    guardrails: max 3 jobs per plan, max 10 tasks per job.

    Args:
        api_client: StewieApiClient instance for API calls.
    """

    MAX_JOBS_PER_PLAN = 3
    MAX_TASKS_PER_JOB = 10
    VALID_ROLES = {"developer", "tester"}

    def __init__(self, api_client: Any):
        self._api = api_client

    def parse_and_create(self, project_id: str, plan_json: dict) -> list[str]:
        """
        Parse a plan JSON and create jobs/tasks via the API.

        Validates the plan first, then creates resources in order:
        1. Validate structure
        2. For each job: create job via API
        3. For each task in job: record for multi-task job creation

        Args:
            project_id: Project UUID.
            plan_json: Parsed LLM plan with 'jobs' array.

        Returns:
            List of created job IDs.

        Raises:
            PlanValidationError: If plan structure is invalid.
        """
        errors = self.validate_plan(plan_json)
        if errors:
            raise PlanValidationError(errors)

        jobs = plan_json.get("jobs", [])
        created_job_ids: list[str] = []

        for job_def in jobs:
            try:
                job_id = self._create_job_with_tasks(project_id, job_def)
                created_job_ids.append(job_id)
                logger.info("Created job %s from plan", job_id)
            except Exception as e:
                logger.error(
                    "Failed to create job '%s': %s",
                    job_def.get("title", "?"), e,
                )
                # Continue creating remaining jobs — partial success is OK
                continue

        logger.info(
            "Plan execution complete: %d/%d jobs created",
            len(created_job_ids), len(jobs),
        )
        return created_job_ids

    def _create_job_with_tasks(self, project_id: str, job_def: dict) -> str:
        """
        Create a single job with its tasks via the API.

        Uses multi-task job creation when there are multiple tasks,
        or single-task creation for simple jobs.

        Args:
            project_id: Project UUID.
            job_def: Job definition from the plan.

        Returns:
            Created job ID.
        """
        tasks = job_def.get("tasks", [])
        title = job_def.get("title", "Untitled Job")

        if len(tasks) <= 1:
            # Single-task mode
            task_desc = tasks[0].get("description", "") if tasks else ""
            result = self._api.create_job(
                project_id=project_id,
                title=title,
                description=task_desc,
            )
            return result.get("id", "")

        # Multi-task DAG mode
        api_tasks = []
        for task_def in tasks:
            client_id = task_def.get("title", str(uuid.uuid4()))
            api_task: dict[str, Any] = {
                "clientId": client_id,
                "objective": task_def.get("title", "Untitled Task"),
                "scope": task_def.get("description", ""),
            }

            # Map dependsOn references
            depends_on = task_def.get("dependsOn", [])
            if depends_on:
                api_task["dependsOn"] = depends_on

            api_tasks.append(api_task)

        result = self._api.create_multi_task_job(
            project_id=project_id,
            tasks=api_tasks,
        )
        return result.get("id", "")

    def validate_plan(self, plan_json: dict) -> list[str]:
        """
        Validate plan structure. Returns list of errors (empty = valid).

        Checks:
        - Plan has a 'jobs' array
        - Job count within guardrail limits
        - Each job has a title
        - Each task has title, description, and valid role
        - Dependency references are valid within the plan
        - Task count per job within limits

        Args:
            plan_json: Parsed plan JSON.

        Returns:
            List of validation error strings. Empty list means valid.
        """
        errors: list[str] = []

        if not isinstance(plan_json, dict):
            errors.append("Plan must be a JSON object")
            return errors

        jobs = plan_json.get("jobs")
        if not isinstance(jobs, list):
            errors.append("Plan must contain a 'jobs' array")
            return errors

        if len(jobs) == 0:
            errors.append("Plan must contain at least one job")
            return errors

        if len(jobs) > self.MAX_JOBS_PER_PLAN:
            errors.append(
                f"Plan exceeds maximum of {self.MAX_JOBS_PER_PLAN} jobs "
                f"(has {len(jobs)})"
            )

        for i, job in enumerate(jobs):
            job_label = f"jobs[{i}]"

            if not isinstance(job, dict):
                errors.append(f"{job_label} must be a JSON object")
                continue

            if not job.get("title"):
                errors.append(f"{job_label} is missing required 'title'")

            tasks = job.get("tasks", [])
            if not isinstance(tasks, list):
                errors.append(f"{job_label}.tasks must be an array")
                continue

            if len(tasks) > self.MAX_TASKS_PER_JOB:
                errors.append(
                    f"{job_label} exceeds maximum of {self.MAX_TASKS_PER_JOB} "
                    f"tasks (has {len(tasks)})"
                )

            # Collect task titles for dependency validation
            task_titles = set()
            for j, task in enumerate(tasks):
                task_label = f"{job_label}.tasks[{j}]"

                if not isinstance(task, dict):
                    errors.append(f"{task_label} must be a JSON object")
                    continue

                if not task.get("title"):
                    errors.append(f"{task_label} is missing required 'title'")
                else:
                    task_titles.add(task["title"])

                if not task.get("description"):
                    errors.append(f"{task_label} is missing required 'description'")

                role = task.get("role", "developer")
                if role not in self.VALID_ROLES:
                    errors.append(
                        f"{task_label} has invalid role '{role}' "
                        f"(must be one of {self.VALID_ROLES})"
                    )

            # Validate dependency references
            for j, task in enumerate(tasks):
                if not isinstance(task, dict):
                    continue
                depends_on = task.get("dependsOn", [])
                if not isinstance(depends_on, list):
                    errors.append(
                        f"{job_label}.tasks[{j}].dependsOn must be an array"
                    )
                    continue
                for dep in depends_on:
                    if dep not in task_titles:
                        errors.append(
                            f"{job_label}.tasks[{j}] references unknown "
                            f"dependency '{dep}'"
                        )

        return errors
