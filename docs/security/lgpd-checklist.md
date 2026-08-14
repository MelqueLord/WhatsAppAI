# LGPD Checklist — WhatsApp AI Manager

**Version:** 1.0  
**Date:** 2026-08-14

## Data Processing Inventory

| Data Type | Purpose | Legal Basis | Retention | Encrypted |
|-----------|---------|-------------|-----------|-----------|
| User email | Authentication | Contract | Account lifetime | Hash (password) |
| Phone numbers | WhatsApp messaging | Legitimate interest | 90 days (configurable) | At rest |
| Message content | Customer service | Consent (end-user) | 90 days (configurable) | At rest |
| API keys | Provider integration | Contract | Account lifetime | AES-256 |
| Audit logs | Security/compliance | Legal obligation | 1 year minimum | No (immutable) |

## Controls Implemented

- [x] Data minimization: only necessary fields collected
- [x] Purpose limitation: data used only for stated purposes
- [x] Storage limitation: retention worker enforces policies
- [x] Integrity: encrypted secrets, sanitized logs
- [x] Confidentiality: tenant isolation, access controls
- [x] Accountability: immutable audit log

## Pending (Requires Legal Review)

- [ ] End-user consent mechanism for message processing
- [ ] Data portability API for end-user data export
- [ ] Right to erasure implementation (anonymization vs deletion)
- [ ] Data Protection Impact Assessment (DPIA) for AI processing
- [ ] Privacy policy text review

## Responsible

- **Technical:** Engineering team
- **Legal:** Pending independent review
- **DPO:** To be designated before pilot
