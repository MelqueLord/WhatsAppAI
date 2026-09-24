# Design: ativação e convites

Convites geram token de uso único e 24 horas de validade, persistido como hash. Ativação de senha e consumo do token acontecem atomicamente. Reenvio substitui convite anterior; desativação invalida sessões e reativação exige novo login.

Fonte: [specs de identidade](../specs/identidade/) e [especificação-base](../specs/plataforma/especificacao-plataforma.md).
