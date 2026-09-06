# Security Policy

## Overview

At **ScrumPulse**, security and data integrity are fundamental to our engineering velocity and agile delivery intelligence platform. We take the security of our distributed agile telemetry, sprint governance data, team rosters, and AI-powered coaching interactions seriously.

We appreciate the efforts of security researchers and community members who help us maintain the highest standards of security and privacy.

---

## Supported Versions

We actively maintain and provide security patches for the following versions of ScrumPulse:

| Version | Supported | Status / Notes |
| :--- | :---: | :--- |
| **1.0.x (`main`)** | :white_check_mark: | Currently active release branch; receives all security updates and vulnerability patches. |
| **< 1.0** | :x: | Legacy preview releases; no longer maintained. Please upgrade to latest `main`. |

---

## Reporting a Vulnerability

If you discover a potential security vulnerability within ScrumPulse, please report it to us responsibly before any public disclosure. We are committed to working with you to verify, address, and resolve issues promptly.

### Preferred Channel: GitHub Private Vulnerability Reporting
The fastest and most secure method to disclose a vulnerability is via GitHub:
1. Navigate to the [ScrumPulse Security Advisories tab](https://github.com/RanjithaSavandaiah/ScrumPulse/security/advisories).
2. Click **"Report a vulnerability"** to open a confidential advisory draft directly with the repository maintainers.

### Alternative Channel: Direct Email
If you are unable to use GitHub Private Vulnerability Reporting, you may report findings via email:
- **Contact**: Ranjitha Savandaiah (`r.savandaiah@devon.nl`)
- **Subject**: `[SECURITY] ScrumPulse Vulnerability Report - <Brief Description>`
- Please include your PGP public key if you wish to receive encrypted communications.

---

## What to Include in Your Report

To help us investigate and triage your report efficiently, please include:

- **Type of issue**: (e.g., SQL injection, XSS, Broken Access Control / RBAC bypass, CSRF, sensitive data exposure, authentication flaw, multi-tenant squad data leakage).
- **Affected component(s)**:
  - Backend Web API (`src/ScrumPulse.Api`, Controllers, Middleware)
  - Business Logic & Domain (`src/ScrumPulse.Application`, `src/ScrumPulse.Domain`)
  - Persistence & Database (`src/ScrumPulse.Infrastructure`, EF Core queries)
  - Frontend Single-Page App (`src/ScrumPulse.UI`, Angular 18, NgRx store)
  - AI Coach Integration (`src/ScrumPulse.AI`, Microsoft Agent Framework)
  - Deployment Artifacts (`Dockerfile`, `render.yaml`, CI/CD pipelines)
- **Step-by-step reproduction steps**: Clear, reproducible instructions or a minimal Proof of Concept (PoC) demonstrating the impact.
- **Expected vs. actual behavior**: Details on what occurred and how it deviates from secure behavior.
- **Remediation ideas**: Suggested code changes, configuration adjustments, or patches (if known).

> [!IMPORTANT]
> **Please do not report security vulnerabilities through public GitHub issues, pull requests, or discussions.** All vulnerability reports must remain private until a resolution is deployed.

---

## Response Timeline & Remediation SLA

We aim to adhere to the following timelines for reported vulnerabilities:

| Stage | Target Timeline | Details |
| :--- | :--- | :--- |
| **Initial Acknowledgment** | Within **24–48 hours** | Maintainer confirms receipt of the vulnerability submission. |
| **Triage & Validation** | Within **3 business days** | Issue is verified, severity assessed (CVSS v3.1), and priority assigned. |
| **Patch & Fix Deployment** | **7–14 days** (Critical/High)<br>**30 days** (Medium/Low) | Security fix developed, tested via automated test matrix, and pushed. |
| **Public Disclosure** | Coordinated | Release notes and GitHub Security Advisory published upon patch release. |

---

## Defense-in-Depth Architecture

ScrumPulse incorporates defense-in-depth security principles across every layer:

- **Role-Based Access Control (RBAC)**: Strict role boundaries (`ScrumMaster`, `Developer`, `QaEngineer`, `Cdl`, `ProductOwner`, `ClientStakeholder`, `AgileCoach`) with a dedicated Scrum Master PIN interception gateway.
- **Multi-Tenant Squad Isolation**: Global query filters enforcing squad boundary isolation (`TeamId`) across work items, standups, blockers, leaves, reviews, and retrospectives.
- **OWASP Security Headers**: Comprehensive middleware configuring `Content-Security-Policy`, `X-Frame-Options: DENY`, `X-Content-Type-Options: nosniff`, `Strict-Transport-Security` (HSTS), and `Permissions-Policy`.
- **Adaptive Rate Limiting**: Sliding-window IP limiters (60 req/min), authentication brute-force mitigations (5 req/min), and bounded AI token buckets.
- **Zero Known Vulnerabilities**: Automated dependency vulnerability scanning in CI/CD via `dotnet list package --vulnerable --include-transitive` and npm security audits.

---

## Responsible Disclosure & Safe Harbor

We consider security research conducted under this policy to be authorized. We commit to:
- **No Legal Action**: We will not initiate legal action against researchers who discover and report vulnerabilities in compliance with these guidelines in good faith.
- **Safe Testing Ground Rules**:
  - Avoid violating privacy, destroying data, or interrupting service availability (e.g., DoS/DDoS attacks).
  - Do not access, modify, or exfiltrate customer or squad data beyond what is strictly necessary to demonstrate proof-of-concept.
  - Give us reasonable time to investigate and address the vulnerability before sharing details publicly.
- **Credit & Recognition**: If you wish, we will gladly credit you in our release notes and GitHub Security Advisory for your responsible disclosure.

Thank you for helping keep ScrumPulse and our engineering community safe!
