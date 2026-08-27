# LGPD Checklist — WhatsApp AI Manager

**Version:** 2.0
**Date:** 2026-08-27

## Data Processing Inventory

| Data Type | Purpose | Legal Basis | Retention | Encrypted |
|-----------|---------|-------------|-----------|-----------|
| User email | Authentication | Contract | Account lifetime | Hash (password) |
| Phone numbers | WhatsApp messaging | Defined per tenant purpose | Configured per purpose | At rest |
| Message content | Customer service | Defined per tenant purpose | Configured per purpose | At rest |
| API keys | Provider integration | Contract | Account lifetime | AES-256 |
| Audit logs | Security/compliance | Legal obligation | 1 year minimum | No (immutable) |

## Controls Implemented

- [x] Data minimization: only necessary fields collected
- [x] Purpose limitation: data used only for stated purposes
- [x] Storage limitation: retention worker enforces policies
- [x] Integrity: encrypted secrets, sanitized logs
- [x] Confidentiality: tenant isolation, access controls
- [x] Accountability: immutable audit log

## Technical Controls Added

- [x] Legal basis and retention recorded per tenant purpose
- [x] Consent evidence and revocation when consent is the selected basis
- [x] Data access/portability export scoped to tenant
- [x] Transactional anonymization for erasure requests
- [x] Denial requires reason and review date
- [x] RIPD and controller/operator matrix versioned
- [x] Public privacy identity sourced from environment, without fictitious defaults

## Operational Inputs Before Public Launch

- [ ] Configure real controller identity and privacy channel
- [ ] Configure DPO identity/contact or documented exemption
- [ ] Review and approve `docs/security/lgpd-ripd.md`
- [ ] Validate each tenant's legal bases and retention periods
- [ ] Obtain privacy policy/legal text review

## Responsible

- **Technical:** Engineering team
- **Legal:** Pending independent review
- **DPO:** Supplied by production configuration, or exemption documented there
