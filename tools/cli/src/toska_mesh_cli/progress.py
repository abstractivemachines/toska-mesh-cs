from __future__ import annotations

import sys
from contextlib import AbstractContextManager
from typing import Optional, TextIO


class ProgressReporter:
    """Minimal progress helper for CLI commands."""

    def __init__(self, *, stream: Optional[TextIO] = None, err_stream: Optional[TextIO] = None):
        self.stream = stream or sys.stdout
        self.err_stream = err_stream or sys.stderr

    def step(self, message: str) -> "ProgressStep":
        return ProgressStep(message, stream=self.stream, err_stream=self.err_stream)


class ProgressStep(AbstractContextManager):
    def __init__(self, message: str, *, stream: TextIO, err_stream: TextIO):
        self.message = message
        self.stream = stream
        self.err_stream = err_stream
        self._status: Optional[str] = None

    def mark(self, status: str) -> None:
        self._status = status

    def __enter__(self) -> "ProgressStep":
        self.stream.write(f"- {self.message} ... ")
        self.stream.flush()
        return self

    def __exit__(self, exc_type, exc_val, exc_tb) -> bool:
        if exc_type is not None:
            status = "fail"
            target = self.err_stream
        else:
            status = self._status or "ok"
            target = self.stream

        target.write(f"{status}\n")
        target.flush()
        return False
