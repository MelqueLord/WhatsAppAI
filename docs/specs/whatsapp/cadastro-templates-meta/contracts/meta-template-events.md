# Contrato externo: templates e eventos da Meta

## Criar template

```http
POST https://graph.facebook.com/v26.0/{WABA_ID}/message_templates
Authorization: Bearer {ACCESS_TOKEN}
Content-Type: application/json
```

```json
{
  "name": "atualizacao_atendimento",
  "language": "pt_BR",
  "category": "UTILITY",
  "parameter_format": "POSITIONAL",
  "components": [
    {
      "type": "BODY",
      "text": "Olá, {{1}}. Seu protocolo {{2}} foi atualizado.",
      "example": {
        "body_text": [["Maria", "ATD-123"]]
      }
    },
    {
      "type": "FOOTER",
      "text": "Equipe de atendimento"
    }
  ]
}
```

Resposta relevante: `id`, `status` e `category`. O adaptador deve tolerar campos adicionais e nunca registrar corpo ou token.

## Listar/reconciliar

```http
GET https://graph.facebook.com/v26.0/{WABA_ID}/message_templates?fields=id,name,language,status,category,parameter_format,components&limit=250
Authorization: Bearer {ACCESS_TOKEN}
```

Seguir somente URLs `paging.next` HTTPS do host confiável da Meta. Para reconciliação de uma submissão, usar filtro por nome quando suportado e confirmar idioma e conteúdo normalizado localmente.

## Status webhook

Campo: `message_template_status_update`.

```json
{
  "object": "whatsapp_business_account",
  "entry": [
    {
      "id": "102290129340398",
      "time": 1751247548,
      "changes": [
        {
          "field": "message_template_status_update",
          "value": {
            "event": "REJECTED",
            "message_template_id": 1689556908129835,
            "message_template_name": "atualizacao_atendimento",
            "message_template_language": "pt_BR",
            "message_template_category": "UTILITY",
            "reason": "INVALID_FORMAT",
            "rejection_info": {
              "reason": "Descrição sanitizada pela aplicação",
              "recommendation": "Orientação sanitizada pela aplicação"
            }
          }
        }
      ]
    }
  ]
}
```

`entry.id` identifica a WABA. O endpoint valida `X-Hub-Signature-256` sobre o corpo original antes de interpretar ou resolver o tenant.

Também assinar:

- `template_category_update`: atualizar categoria efetiva ou provocar sincronização autoritativa.
- `message_template_components_update`: sincronizar os componentes e recalcular compatibilidade.

## Classificação de falhas

| Resposta | Tratamento |
|---|---|
| `400`, Graph code `100` | validação permanente; não repetir automaticamente |
| Graph subcode `2388024` | possível duplicidade; reconciliar por nome/idioma e comparar conteúdo |
| `403` ou Graph code `200` | permissão insuficiente; orientar `whatsapp_business_management`/Advanced Access |
| `429` | retry com backoff e respeito a cabeçalhos; reconciliar antes se o aceite for incerto |
| `5xx` antes de enviar | retry com backoff |
| timeout/conexão interrompida durante/após envio | `OutcomeUnknown`; reconciliar antes de repetir |

Mensagens retornadas ao usuário são mapeadas e sanitizadas. A resposta bruta pode ser usada transitoriamente pelo adaptador, mas não é persistida nem registrada.
