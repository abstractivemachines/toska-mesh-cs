PYTHON ?= python3
VENV ?= .venv
VENV_BIN := $(VENV)/bin

.PHONY: install format lint typecheck test check

install:
	$(PYTHON) -m venv $(VENV)
	$(VENV_BIN)/pip install -e .[dev]

format:
	$(VENV_BIN)/black src tests

lint:
	$(VENV_BIN)/ruff check src tests

typecheck:
	$(VENV_BIN)/mypy src tests

test:
	$(VENV_BIN)/pytest --cov=toska_mesh_cli --cov-report=term-missing

check: format lint typecheck test
