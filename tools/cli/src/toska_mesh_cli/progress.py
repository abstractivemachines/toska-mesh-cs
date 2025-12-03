from __future__ import annotations

import time
import sys
from contextlib import AbstractContextManager
from typing import Optional, TextIO

try:
    from rich.console import Console
    from rich.status import Status
except Exception:  # pragma: no cover - optional dependency
    Console = None
    Status = None


class ProgressReporter:
    """Progress helper with optional rich rendering."""

    def __init__(self, *, stream: Optional[TextIO] = None, err_stream: Optional[TextIO] = None):
        self.stream = stream or sys.stdout
        self.err_stream = err_stream or sys.stderr
        self._steps: list[tuple[str, str, float]] = []
        self.console: Console | None = None
        if Console and hasattr(self.stream, "isatty") and self.stream.isatty():
            self.console = Console(file=self.stream, force_terminal=False)

    def step(self, message: str) -> "ProgressStep":
        return ProgressStep(message, reporter=self)

    def summarize(self, *, header: str = "Summary") -> None:
        if not self._steps:
            return
        total = len(self._steps)
        ok = sum(1 for _, status, _ in self._steps if status == "ok")
        failed = sum(1 for _, status, _ in self._steps if status == "fail")
        skipped = sum(1 for _, status, _ in self._steps if status == "skipped")
        duration = sum(duration for _, _, duration in self._steps)
        line = f"{header}: {ok} ok, {skipped} skipped, {failed} failed, {total} total in {duration:.1f}s"
        if self.console:
            style = "green" if failed == 0 else "red"
            self.console.print(f"[{style}]{line}[/{style}]")
        else:
            self.stream.write(f"{line}\n")
            self.stream.flush()


class ProgressStep(AbstractContextManager):
    def __init__(self, message: str, *, reporter: ProgressReporter):
        self.message = message
        self.reporter = reporter
        self.stream = reporter.stream
        self.err_stream = reporter.err_stream
        self._status: Optional[str] = None
        self._start = 0.0
        self._rich_status: Status | None = None

    def mark(self, status: str) -> None:
        self._status = status

    def __enter__(self) -> "ProgressStep":
        self._start = time.monotonic()
        if self.reporter.console and Status:
            self._rich_status = self.reporter.console.status(self.message, spinner="dots")
            self._rich_status.__enter__()
        else:
            self.stream.write(f"- {self.message} ... ")
            self.stream.flush()
        return self

    def __exit__(self, exc_type, exc_val, exc_tb) -> bool:
        duration = time.monotonic() - self._start
        if exc_type is not None:
            status = "fail"
            target = self.err_stream
        else:
            status = self._status or "ok"
            target = self.stream

        if self._rich_status:
            self._rich_status.__exit__(exc_type, exc_val, exc_tb)
            style = {"ok": "green", "skipped": "yellow", "fail": "red"}.get(status, "white")
            self.reporter.console.print(f"[{style}]{status}[/] {self.message} ({duration:.1f}s)")
        else:
            target.write(f"{status} ({duration:.1f}s)\n")
            target.flush()

        self.reporter._steps.append((self.message, status, duration))
        return False
