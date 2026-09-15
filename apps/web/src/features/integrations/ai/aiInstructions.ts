export const MAX_INSTRUCTIONS_CHARACTERS = 4000

interface CompanyAiProfile {
  businessType: string
  businessDescription: string
  targetAudience: string
  serviceCatalog: string
  toneOfVoice: string
  serviceHours: string
  location: string
  serviceGoal: string
  qualificationData: string
  serviceProcess: string
  handoffCriteria: string
}

export function buildAiInstructions(profile: CompanyAiProfile, directions: string) {
  const clean = (value: string) => value.replace(/[\r\n]+/g, ' ').trim()
  const profileText = [
    `Tipo de negócio: ${clean(profile.businessType || 'Não informado')}`,
    `Descrição do negócio: ${clean(profile.businessDescription)}`,
    `Público-alvo: ${clean(profile.targetAudience)}`,
    `Produtos e serviços: ${clean(profile.serviceCatalog)}`,
    `Tom de voz: ${clean(profile.toneOfVoice)}`,
    `Horário de atendimento: ${clean(profile.serviceHours)}`,
    `Localização: ${clean(profile.location)}`,
    `Objetivo do atendimento: ${clean(profile.serviceGoal)}`,
    `Dados para qualificar: ${clean(profile.qualificationData)}`,
    `Processo de atendimento: ${clean(profile.serviceProcess)}`,
    `Critérios de encaminhamento: ${clean(profile.handoffCriteria)}`,
  ].join('\n')

  return `[PERFIL_EMPRESA]\n${profileText}\n[/PERFIL_EMPRESA]\n\n${directions.trim()}`
}
