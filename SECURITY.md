# Security policy

## Reporting a vulnerability

Please report security issues privately through GitHub Security Advisories:
<https://github.com/Cahard/ZTorrent/security/advisories/new>.

Do not open a public issue for path traversal, arbitrary file write/delete,
remote code execution, credential leakage, or denial-of-service findings.

Include:

- affected version and commit;
- a minimal reproduction (a torrent file or magnet where possible);
- expected and actual behaviour;
- impact assessment.

We aim to acknowledge reports within 72 hours and provide a remediation plan
within 7 days. Please do not disclose the issue publicly before a fix is
available.

## Scope

The client processes untrusted `.torrent`/magnet metadata and writes files to
disk. Reports about path traversal, UNC/ADS handling, unsafe deletion, or
private-torrent leaks are especially valuable.

## Out of scope

- torrents you do not have the right to distribute;
- vulnerabilities in third-party trackers or peers;
- social engineering;
- issues that require an already-compromised Windows account.
