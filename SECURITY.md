# Security Policy

## Reporting a Vulnerability

If you discover a security vulnerability in Toska Mesh, please report it through GitHub's private vulnerability reporting feature:

1. Go to the repository's **Security** tab
2. Click **Report a vulnerability**
3. Provide a detailed description of the vulnerability

We will acknowledge your report and work with you to understand and address the issue. Please do not disclose the vulnerability publicly until we have had a chance to address it.

## Supported Versions

Security updates are provided for the latest release only.

## Security Best Practices

When deploying Toska Mesh:

- Keep `MESH_SERVICE_AUTH_SECRET` confidential and use a strong 32+ character value
- Store secrets (JWT keys, connection strings, TLS certificates) outside of source control
- Use environment variables or secret management systems for sensitive configuration
- Review and restrict network access to internal services (Consul, Redis, PostgreSQL)
- Keep all dependencies updated to their latest stable versions
