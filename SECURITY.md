# Security Policy

## Supported versions

This repo targets Umbraco 17 / .NET 10 and ships `Jumoo.TranslationManager.Automate`, versioned
and released from `main`.

| Version | Branch | Supported |
| --- | --- | --- |
| 17.x | `main` | Yes |

## Reporting a vulnerability

Please **do not** open a public issue for a security problem.

Email **info@jumoo.co.uk** with a description of the issue, the version affected, and steps
to reproduce it. We'll acknowledge within a few working days and keep you updated as we
work on it.

This package creates, submits and approves Translation Manager jobs, and can publish content,
from Umbraco Automate workflows - so if the issue involves how a workflow step resolves
permissions, how the service account identity is attributed, or anything that could let an
automation run with more access than intended, say so - those get sequenced first.
