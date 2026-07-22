# Security Policy

## Supported Versions

Square is pre-1.0 experimental software. Security fixes are applied to the latest revision of the `main` branch. Older commits and local package builds are not supported.

## Reporting a Vulnerability

Do not open a public issue for a suspected vulnerability.

Use GitHub's private vulnerability reporting for this repository: open the repository's **Security** tab and choose **Report a vulnerability**. Include reproduction steps, affected components, impact, and any suggested mitigation.

Expect an initial acknowledgement within seven days. A fix timeline depends on severity and the maturity of the affected experimental feature. Please allow time for a coordinated fix before public disclosure.

## DevTools Service

`Square.DevTools` is a local development service. Keep it bound to loopback, use a generated access token, and do not expose it to untrusted networks. Input injection, inspector data, source paths, and text content should be enabled only when required.
