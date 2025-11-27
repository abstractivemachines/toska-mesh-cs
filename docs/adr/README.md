# Architecture Decision Records (ADRs)

This directory contains Architecture Decision Records for the Toska Mesh C# implementation.

## What is an ADR?

An ADR is a document that captures an important architectural decision made along with its context and consequences.

## ADR Index

| ID | Title | Status | Date |
|----|-------|--------|------|
| [001](001-use-orleans-for-clustering.md) | Use Orleans for Clustering | Accepted | 2025-11-27 |
| [002](002-use-polly-for-resilience.md) | Use Polly for Resilience | Accepted | 2025-11-27 |
| [003](003-consul-service-discovery.md) | Use Consul for Service Discovery | Accepted | 2025-11-27 |
| [004](004-yarp-api-gateway.md) | Use YARP for API Gateway | Accepted | 2025-11-27 |

## Template

When creating a new ADR, use this template:

```markdown
# ADR-NNN: Title

## Status
Proposed | Accepted | Deprecated | Superseded

## Context
What is the issue that we're seeing that is motivating this decision or change?

## Decision
What is the change that we're proposing and/or doing?

## Consequences
What becomes easier or more difficult to do because of this change?
```
