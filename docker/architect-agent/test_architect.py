"""
Tests for the Architect Agent Python modules.

Validates plan extraction, context building, job parsing validation,
and mock LLM mode — all without network or RabbitMQ dependencies.

REF: JOB-022 T-199
"""

import json
import sys
import os
import unittest
from unittest.mock import MagicMock

# Mock pika before importing architect modules (pika only in Docker)
pika_mock = MagicMock()
sys.modules["pika"] = pika_mock
sys.modules["pika.exceptions"] = MagicMock()

# Ensure docker/architect-agent is on the path
sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "..", "docker", "architect-agent"))

from architect_main import extract_plan_json, _mock_llm_response
from context_builder import ContextBuilder
from job_parser import JobParser, PlanValidationError


class TestExtractPlanJson(unittest.TestCase):
    """Tests for JSON plan extraction from LLM responses."""

    def test_extract_from_code_block(self):
        """Should extract JSON from a markdown code block."""
        response = '''Here is my plan:

```json
{
  "summary": "Test plan",
  "jobs": [
    {
      "title": "Test Job",
      "tasks": [
        {
          "title": "Task 1",
          "description": "Do something",
          "role": "developer"
        }
      ]
    }
  ]
}
```

That's the plan.'''

        result = extract_plan_json(response)
        self.assertIsNotNone(result)
        self.assertEqual(result["summary"], "Test plan")
        self.assertEqual(len(result["jobs"]), 1)
        self.assertEqual(result["jobs"][0]["title"], "Test Job")

    def test_extract_from_bare_json(self):
        """Should extract bare JSON when no code block is present."""
        response = 'Here is the plan: {"summary": "Bare plan", "jobs": []}'

        result = extract_plan_json(response)
        self.assertIsNotNone(result)
        self.assertEqual(result["summary"], "Bare plan")

    def test_no_json_returns_none(self):
        """Should return None when no JSON is found."""
        result = extract_plan_json("No JSON here, just plain text.")
        self.assertIsNone(result)

    def test_invalid_json_returns_none(self):
        """Should return None for malformed JSON."""
        result = extract_plan_json('```json\n{invalid json}\n```')
        self.assertIsNone(result)


class TestMockLlmResponse(unittest.TestCase):
    """Tests for mock LLM response generation."""

    def test_mock_produces_valid_json(self):
        """Mock response should contain extractable JSON."""
        response = _mock_llm_response("Test prompt")
        plan = extract_plan_json(response)
        self.assertIsNotNone(plan)
        self.assertIn("jobs", plan)
        self.assertTrue(len(plan["jobs"]) > 0)

    def test_mock_plan_has_valid_structure(self):
        """Mock plan should pass validation."""
        response = _mock_llm_response("Test prompt")
        plan = extract_plan_json(response)
        parser = JobParser(api_client=None)
        errors = parser.validate_plan(plan)
        self.assertEqual(errors, [], f"Validation errors: {errors}")


class TestJobParserValidation(unittest.TestCase):
    """Tests for plan validation in JobParser."""

    def setUp(self):
        self.parser = JobParser(api_client=None)

    def test_valid_plan_passes(self):
        """A well-formed plan should have no validation errors."""
        plan = {
            "summary": "Test",
            "jobs": [{
                "title": "Job 1",
                "tasks": [{
                    "title": "Task 1",
                    "description": "Do the thing",
                    "role": "developer"
                }]
            }]
        }
        errors = self.parser.validate_plan(plan)
        self.assertEqual(errors, [])

    def test_missing_jobs_array(self):
        """Plan without jobs array should fail."""
        errors = self.parser.validate_plan({"summary": "No jobs"})
        self.assertTrue(any("jobs" in e for e in errors))

    def test_empty_jobs_array(self):
        """Plan with empty jobs should fail."""
        errors = self.parser.validate_plan({"jobs": []})
        self.assertTrue(any("at least one job" in e for e in errors))

    def test_too_many_jobs(self):
        """Plan exceeding job limit should fail."""
        plan = {
            "jobs": [
                {"title": f"Job {i}", "tasks": [{"title": "T", "description": "D", "role": "developer"}]}
                for i in range(5)
            ]
        }
        errors = self.parser.validate_plan(plan)
        self.assertTrue(any("maximum" in e.lower() for e in errors))

    def test_missing_task_title(self):
        """Task without title should fail."""
        plan = {
            "jobs": [{
                "title": "Job 1",
                "tasks": [{"description": "D", "role": "developer"}]
            }]
        }
        errors = self.parser.validate_plan(plan)
        self.assertTrue(any("title" in e for e in errors))

    def test_invalid_role(self):
        """Task with invalid role should fail."""
        plan = {
            "jobs": [{
                "title": "Job 1",
                "tasks": [{"title": "T", "description": "D", "role": "manager"}]
            }]
        }
        errors = self.parser.validate_plan(plan)
        self.assertTrue(any("role" in e for e in errors))

    def test_invalid_dependency_reference(self):
        """Task referencing non-existent dependency should fail."""
        plan = {
            "jobs": [{
                "title": "Job 1",
                "tasks": [{
                    "title": "Task A",
                    "description": "D",
                    "role": "developer",
                    "dependsOn": ["NonExistent"]
                }]
            }]
        }
        errors = self.parser.validate_plan(plan)
        self.assertTrue(any("unknown dependency" in e.lower() for e in errors))


class TestContextBuilder(unittest.TestCase):
    """Tests for ContextBuilder."""

    def test_token_estimation(self):
        """Token estimate should be roughly text_length / 4."""
        text = "a" * 400
        tokens = ContextBuilder.estimate_tokens(text)
        self.assertEqual(tokens, 100)

    def test_truncation_within_budget(self):
        """Text within budget should not be truncated."""
        text = "Short text"
        result = ContextBuilder.truncate_to_budget(text, max_tokens=100)
        self.assertEqual(result, text)

    def test_truncation_over_budget(self):
        """Text over budget should be truncated from the beginning."""
        text = "x" * 4000  # 1000 tokens
        result = ContextBuilder.truncate_to_budget(text, max_tokens=500)
        self.assertTrue(len(result) < len(text))
        self.assertIn("truncated", result.lower())


if __name__ == "__main__":
    unittest.main()
