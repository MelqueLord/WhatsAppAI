# Data Model: LGPD Production Readiness

## ProcessingPurpose

- `Id`, `TenantId`
- `Name` (único por tenant), `Description`
- `LegalBasis`: Consent, Contract, LegalObligation, LegitimateInterest, CreditProtection, RightsExercise, LifeProtection, HealthProtection, PublicPolicy, Research
- `RetentionDays` (1–3650), `IsActive`
- `CreatedByUserId`, `CreatedAt`, `UpdatedByUserId`, `UpdatedAt`

## ConsentEvidence

- `Id`, `TenantId`, `ContactId`, `ProcessingPurposeId`
- `Source` (descrição curta, sem conteúdo bruto), `EvidenceReference` opcional
- `GrantedAt`, `RevokedAt`, `RecordedByUserId`, `CreatedAt`
- Uma evidência ativa por contato/finalidade; somente finalidade com base `Consent` aceita criação.

## DataSubjectRequest

- `Id`, `TenantId`, `ContactId`
- `Type`: Access, Portability, Correction, Anonymization, Blocking, Erasure
- `Status`: Open, Completed, Denied
- `RequestedByUserId`, `RequestedAt`, `DueAt`
- `ResolvedByUserId`, `ResolvedAt`, `DecisionReason`, `ReviewAt`
- Transições: Open → Completed; Open → Denied. Uma solicitação finalizada é imutável.

## Anonimização

- `Contact.PhoneNumber` vira identificador não reversível local (`anon-{ContactId:N}`); nome e foto ficam nulos.
- Conteúdo, caption, IDs/URLs de mídia e identificadores externos das mensagens ficam nulos.
- A solicitação e auditoria preservam IDs, tipo, resultado e datas, sem cópia de PII.
- Registros criptografados de webhook continuam sujeitos ao worker de retenção existente; o fluxo não tenta localizar PII dentro de payload opaco.
