# MSR Financial Engine

Motor de Reconciliação Financeira Multi-Fonte.

Ingere extratos bancários, faturas, notas fiscais e dados de ERPs em formatos diferentes,
normaliza tudo para um modelo canônico, casa transações automaticamente (regras
determinísticas + fuzzy matching) e sinaliza divergências para revisão humana.

Stack: **.NET 8** (ASP.NET Core Web API) + **PostgreSQL** + EF Core.

A arquitetura detalhada está em [docs/architecture.md](docs/architecture.md).

## Como rodar

### Opção 1 — tudo em containers

```bash
docker compose up --build
```

A API sobe em `http://localhost:8080` e o Swagger em `http://localhost:8080/swagger`.

### Opção 2 — Postgres em container, API local

```bash
docker compose up -d postgres
dotnet run --project src/MSRFinancialEngine.Api
```

As migrations são aplicadas automaticamente na inicialização.

A aplicação **recusa iniciar** sem `Jwt:SigningKey` (mínimo 32 bytes) — segredo não fica
versionado. Para rodar localmente fora do Docker:

```bash
export Jwt__SigningKey="uma-chave-longa-de-no-minimo-32-bytes"
export SeedAdmin__Password="Admin@123456"
dotnet run --project src/MSRFinancialEngine.Api
```

Na primeira execução, se não houver nenhum usuário, é criado um administrador com o
e-mail de `SeedAdmin:Email` e essa senha. Troque-a antes de qualquer uso real.

### Testes

```bash
dotnet test
```

## Autenticação

Toda a API exige token, exceto `POST /api/auth/login` e `GET /health`.

```bash
TOKEN=$(curl -s -X POST $BASE/auth/login -H "Content-Type: application/json" \
  -d '{"email":"admin@msrfinancialengine.local","password":"Admin@123456"}' | jq -r .accessToken)

curl $BASE/transactions -H "Authorization: Bearer $TOKEN"
```

O login devolve dois tokens: `accessToken` (60 min, usado nas chamadas) e `refreshToken`
(7 dias, usado só para renovar). O admin criado na primeira execução vem com
`mustChangePassword: true` — troque a senha inicial antes de qualquer uso real.

### Sessão

```bash
# renovar (o refresh token usado é revogado e substituído)
curl -X POST $BASE/auth/refresh -H "Content-Type: application/json" \
  -d "{\"refreshToken\":\"$REFRESH\"}"

# encerrar a sessão
curl -X POST $BASE/auth/logout -H "Content-Type: application/json" \
  -d "{\"refreshToken\":\"$REFRESH\"}"

# trocar a própria senha (encerra as demais sessões)
curl -X POST $BASE/auth/change-password -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"currentPassword":"Admin@123456","newPassword":"NovaSenhaSegura@2026"}'
```

Refresh tokens são **rotacionados**: cada renovação revoga o token anterior. Se um token
já revogado for reapresentado — sinal de vazamento — **todas as sessões do usuário são
derrubadas**, inclusive a legítima.

### Proteção contra força bruta

Duas camadas, configuráveis na seção `Auth`:

| Configuração | Padrão | O que protege |
|---|---|---|
| `MaxFailedLoginAttempts` | 5 | Bloqueia a **conta** após N falhas consecutivas |
| `LockoutMinutes` | 15 | Duração do bloqueio |
| `LoginRateLimitPerMinute` | 10 | Limita o **IP de origem** (responde 429) |

O bloqueio por conta protege um alvo específico; o rate limit por IP cobre varredura de
e-mails, que o primeiro não pegaria. Um login bem-sucedido zera o contador de falhas.

O token carrega usuário, papel e empresa. Duas consequências:

- **Decisões são sempre do usuário autenticado.** `POST /divergences/{id}/decide` não
  aceita `userId` no corpo — não é possível decidir em nome de outra pessoa.
- **A empresa vem do token**, não de parâmetro do cliente. Um usuário preso à empresa A
  não enxerga dados da empresa B nem passando `companyId=B` nem forjando o header
  `X-Company-Id` (que só é aceito de `Admin` sem empresa fixa).

## Paginação

As listagens retornam páginas, nunca a tabela inteira:

```bash
curl "$BASE/transactions?companyId=<ID>&page=1&pageSize=100" -H "Authorization: Bearer $TOKEN"
```

```json
{ "items": [], "page": 1, "pageSize": 100, "totalItems": 0, "totalPages": 0 }
```

`pageSize` é limitado a 200 — pedidos acima disso são reduzidos, não recusados.

## Formatos de importação suportados

Cada fonte tem um parser próprio que implementa `ISourceImporter`. Adicionar um novo
formato é criar uma implementação e registrá-la na DI — o núcleo de normalização e
matching não muda.

| `SourceType` | Valor | Formato | Config (`ConfigJson`) |
|---|---|---|---|
| `BankStatementCsv` | 0 | Extrato bancário CSV | `{"delimiter":",","dateFormat":"yyyy-MM-dd","hasHeader":true,"defaultCurrency":"BRL"}` |
| `BankStatementOfx` | 1 | Extrato bancário OFX | `{"defaultCurrency":"BRL"}` |
| `ErpJson` | 2 | Exportação de ERP em JSON | `{}` |
| `InvoiceXmlNfe` | 3 | Nota Fiscal Eletrônica (XML) | `{"defaultCurrency":"BRL"}` |
| `BankStatementMt940` | 4 | Extrato bancário SWIFT MT940 | `{"defaultCurrency":"BRL"}` |

Fontes e empresas podem ser corrigidas depois de criadas (`PUT /api/sources/{id}`,
`PUT /api/companies/{id}`) — inclusive o `ConfigJson` do parser, sem precisar criar outra
fonte. Desativar uma fonte (`"active": false`) faz o sistema **recusar novas importações**
dela, síncronas ou assíncronas: uma origem descomissionada não deve voltar a alimentar
o motor por engano.

Reimportar o mesmo arquivo é seguro: a idempotência é garantida por um hash SHA-256
de empresa + valor + moeda + data + documento + conta + descrição.

## Fluxo de uso

```bash
BASE=http://localhost:8080/api

# 1. Empresa
curl -X POST $BASE/companies -H "Content-Type: application/json" \
  -d '{"name":"Empresa Demo","taxId":"12345678000199","baseCurrencyCode":"BRL"}'

# 2. Fontes (uma por origem de dados)
curl -X POST $BASE/sources -H "Content-Type: application/json" \
  -d '{"companyId":"<COMPANY_ID>","name":"Extrato Banco","type":0,"configJson":"{}"}'

# 3. Importar arquivos
curl -X POST $BASE/import/<SOURCE_ID> -F "file=@extrato.csv"

# 4. Regra de matching
curl -X POST $BASE/matchingrules -H "Content-Type: application/json" \
  -d '{"companyId":"<COMPANY_ID>","name":"Deterministico","type":0,
       "configJson":"{\"toleranceAmount\":0,\"toleranceDays\":2}","priority":1}'

# 5. Rodar o motor de reconciliação
curl -X POST $BASE/matching/run/<COMPANY_ID>

# 6. Tratar as divergências que sobraram
curl $BASE/divergences
curl -X POST $BASE/divergences/<ID>/assign -H "Content-Type: application/json" \
  -d '{"userId":"<USER_ID>"}'
curl -X POST $BASE/divergences/<ID>/decide -H "Content-Type: application/json" \
  -d '{"userId":"<USER_ID>","decision":1,"matchedTransactionId":"<TX_ID>","notes":"casado manualmente"}'

# 7. Relatórios
curl $BASE/reports/reconciliation-rate/<COMPANY_ID>
```

## Regras de matching

Regras são dados configuráveis por empresa, executadas em ordem de `Priority`.

**Determinística** (`type: 0`) — casa por chave exata; `ReferenceDoc` igual gera score
1.0, valor+data dentro da tolerância gera 0.95.

```json
{"toleranceAmount": 0, "toleranceDays": 2}
```

**Fuzzy** (`type: 1`) — para o que sobrou, combina similaridade textual da descrição
(60%) com proximidade de valor (25%) e de data (15%).

```json
{"minScore": 0.75, "toleranceAmount": 0.05, "toleranceDays": 3}
```

Pares com score ≥ 0.98 são auto-aprovados. Abaixo disso viram candidatos pendentes de
revisão, e transações sem par viram divergências.

### Convenção de sinal

Por padrão as regras comparam valores com o mesmo sinal, que é o caso de extrato x ERP.

Para reconciliar **documento contra pagamento** — uma nota fiscal a pagar de `+1250,75`
contra o débito de `-1250,75` no extrato — habilite `matchOppositeSigns`, que exige
sinais opostos e compara as magnitudes:

```json
{"toleranceAmount": 0, "toleranceDays": 2, "matchOppositeSigns": true}
```

A opção existe nos dois tipos de regra.

## Multi-empresa

O isolamento é aplicado na camada de acesso a dados, não em cada consulta. Envie o
header `X-Company-Id` e todas as entidades multi-empresa passam a ser filtradas
automaticamente por query filters globais do EF Core:

```bash
curl $BASE/transactions -H "X-Company-Id: <COMPANY_ID>"
```

Sem o header, nenhum filtro é aplicado (jobs internos e operações administrativas).
Para cruzar empresas deliberadamente no código, use `IgnoreQueryFilters()`.

## Multi-moeda

Taxas de câmbio são armazenadas por data. A conversão usa a taxa vigente na data da
transação — nunca a taxa atual — para que a reconciliação permaneça auditável.

```bash
curl -X POST $BASE/exchangerates -H "Content-Type: application/json" \
  -d '{"currencyCode":"USD","baseCurrencyCode":"BRL","date":"2026-01-10","rateToBase":5.42}'
```

Para reconciliar entre moedas, habilite `crossCurrency` na regra. O motor converte cada
lado para a moeda base da empresa e compara lá — uma fatura de USD 100 casa com um
pagamento de BRL 500 quando a taxa do dia é 5,00:

```json
{"toleranceAmount": 0, "toleranceDays": 2, "crossCurrency": true}
```

Transação em moeda estrangeira sem taxa cadastrada para a data não é casada às cegas:
vira uma divergência com motivo `CurrencyMismatch`, e o resultado da execução informa
quantas ficaram nessa situação (`missingExchangeRates`).

## Papéis e alçada de aprovação

| Papel | Valor | Pode receber divergência | Pode decidir |
|---|---|---|---|
| `Viewer` | 0 | não | não |
| `Analyst` | 1 | sim | não |
| `Approver` | 2 | sim | dentro da alçada |
| `Admin` | 3 | sim | sem limite |

`approvalLimitAmount` define a alçada do aprovador (nulo = sem limite), comparada contra
o valor absoluto da transação. Tentativas fora da regra retornam **403** explicando o
motivo, em vez de falharem silenciosamente.

```bash
curl -X POST $BASE/users -H "Content-Type: application/json" \
  -d '{"name":"Ana","email":"ana@empresa.com","role":2,"approvalLimitAmount":10000}'
```

## Importação assíncrona

Para arquivos grandes, enfileire em vez de esperar na requisição. A API responde **202**
imediatamente e processa em segundo plano:

```bash
curl -X POST $BASE/import/<SOURCE_ID>/async -H "Authorization: Bearer $TOKEN" \
  -F "file=@extrato-grande.csv"
# => 202 { "id": "...", "status": "Queued", ... }

curl $BASE/import/jobs/<JOB_ID> -H "Authorization: Bearer $TOKEN"
# => { "status": "Completed", "totalParsed": 5000, "imported": 5000, "duplicates": 0 }
```

**A tabela de jobs é a fila.** Isso tem três consequências práticas:

- **Sobrevive a reinícios.** Um job enfileirado não se perde quando a aplicação cai.
- **Serve várias instâncias.** A reserva usa `FOR UPDATE SKIP LOCKED`, então cada
  instância pega um job diferente e nenhum arquivo é processado duas vezes.
- **Jobs órfãos voltam à fila.** Se a instância morre no meio do processamento, o job
  fica preso em `Running`; na inicialização, os mais antigos que `StaleJobMinutes` são
  devolvidos à fila. Reprocessar é seguro porque a importação é idempotente por hash —
  o que já entrou volta como duplicata, não em dobro.

Um arquivo malformado termina como `Failed` com a mensagem do erro, em vez de sumir sem
deixar rastro para quem o enviou.

```json
"ImportWorker": { "PollSeconds": 10, "StaleJobMinutes": 30 }
```

## Gestão de usuários

```bash
# desligar quem saiu da empresa (encerra as sessões na hora)
curl -X POST $BASE/users/<USER_ID>/deactivate -H "Authorization: Bearer $TOKEN"

curl -X POST $BASE/users/<USER_ID>/reactivate -H "Authorization: Bearer $TOKEN"

# senha provisória: nasce marcada para troca no próximo acesso
curl -X POST $BASE/users/<USER_ID>/reset-password -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" -d '{"newPassword":"Provisoria@2026"}'
```

Desativar **revoga as sessões ativas** — sem isso o desligado continuaria operando até o
refresh token expirar. Um administrador não pode desativar a si mesmo. Todas essas
operações são restritas a `Admin` e registradas na auditoria.

## Reprocessamento

Quando um parser muda, invalide a fonte e reimporte o arquivo original:

```bash
curl -X POST $BASE/import/<SOURCE_ID>/invalidate
curl -X POST $BASE/import/<SOURCE_ID> -F "file=@extrato.csv"
```

A invalidação remove as transações **não reconciliadas** daquela fonte e suas
divergências; as já reconciliadas são preservadas para não destruir conciliações
aprovadas nem o rastro de auditoria. Sem invalidar, a reimportação é bloqueada pela
idempotência de hash.

## Operação

`GET /health` verifica a aplicação e a conectividade com o Postgres — use como
health check de container ou load balancer.

### Rastreabilidade

Toda resposta traz `X-Correlation-Id`, presente em todas as linhas de log da requisição.
Se o cliente enviar o header, ele é preservado — assim uma chamada que atravessa
serviços mantém o mesmo identificador ponta a ponta:

```bash
curl -D - $BASE/transactions -H "Authorization: Bearer $TOKEN" \
  -H "X-Correlation-Id: pedido-12345"
```

### Métricas

Publicadas via `Meter` do .NET (`MSRFinancialEngine`), coletáveis por OpenTelemetry ou
qualquer listener, sem prender o código a um exportador:

| Métrica | O que indica |
|---|---|
| `msr.transactions.imported` | Volume normalizado por empresa |
| `msr.transactions.auto_reconciled` | Trabalho que o motor poupou do time |
| `msr.divergences.created` | Fila de revisão manual sendo gerada |
| `msr.decisions.recorded` | Decisões manuais, por tipo |
| `msr.exchange_rates.missing` | Taxas de câmbio faltando |
| `msr.matching.duration` | Duração das execuções do motor |

**Sobre exportação:** os pacotes `OpenTelemetry.Exporter.OpenTelemetryProtocol` e
`OpenTelemetry.Api` carregam vulnerabilidades conhecidas em todas as versões publicadas
até hoje ([GHSA-4625-4j76-fww9](https://github.com/advisories/GHSA-4625-4j76-fww9),
[GHSA-g94r-2vxg-569j](https://github.com/advisories/GHSA-g94r-2vxg-569j)), então não são
uma dependência deste projeto — não vale embarcar vulnerabilidade conhecida num sistema
financeiro por conveniência de fiação.

Como as métricas usam o `Meter` padrão do .NET, elas são coletáveis sem esses pacotes:
use a auto-instrumentação do OpenTelemetry Collector (que roda fora do processo) ou um
`MeterListener` próprio. Quando os avisos forem resolvidos upstream, ligar o exportador
no processo é acrescentar o pacote e algumas linhas em `Program.cs`.

### Retenção

Um worker diário expurga o que cresce indefinidamente:

```json
"Retention": { "RefreshTokenDays": 30, "ImportJobDays": 90, "AuditEventDays": 0 }
```

Só saem refresh tokens já revogados ou vencidos, e jobs já finalizados — nunca um job
em andamento ou na fila, por mais antigo que seja.

`AuditEventDays: 0` **desativa** o expurgo da auditoria, e é o padrão de propósito:
auditoria é dado de conformidade, e apagá-la sem uma política explícita do negócio
destruiria justamente a evidência que o sistema existe para produzir.

### Arquivamento da auditoria

Mesmo com `AuditEventDays` configurado, **só sai da base o que já foi copiado para
fora dela** — a segurança é por construção, não por confiar que alguém arquivou antes:

```bash
# copia o período para um arquivo JSON Lines fora do banco
curl -X POST $BASE/reports/audit-archive -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"fromUtc":"2026-01-01T00:00:00","toUtc":"2026-01-31T23:59:59"}'

# confere se a cópia ainda bate com o hash registrado
curl $BASE/reports/audit-archive/<ID>/verify -H "Authorization: Bearer $TOKEN"
```

Cada arquivo guarda o SHA-256 do conteúdo, então adulteração posterior é detectável.
Períodos que alcançam o presente são recusados, e rearquivar um período já coberto não
duplica nada — o controle é **por evento**, não por janela: cada evento copiado recebe
uma marca `ArchivedAtUtc`, e só o que está marcado pode ser expurgado. Um evento que
chegue atrasado, com data dentro de um período já arquivado, é alcançado na passada
seguinte em vez de ser dado como copiado sem estar no arquivo.

Para não depender de alguém disparar o endpoint, ligue o arquivamento automático:

```json
"Retention": { "AutoArchiveAudit": true, "AuditArchiveLagDays": 1 }
```

O worker arquiva antes de expurgar (a ordem inversa não removeria nada) e a defasagem
evita fechar uma janela que ainda pode receber eventos atrasados.

Configure `Retention:AuditArchivePath` para um **volume durável** — arquivar num disco
efêmero que some com o contêiner não arquiva nada.

### Execuções concorrentes do matching

`POST /matching/run/{companyId}` é exclusivo por empresa: uma segunda chamada enquanto a
primeira roda recebe **409**, em vez de reconciliar o mesmo conjunto duas vezes. A trava
usa advisory lock do PostgreSQL, então vale entre instâncias da aplicação — não apenas
dentro de um processo.

## Auditoria

Toda ação relevante (importação, match automático, atribuição, decisão manual) gera um
evento imutável em `audit_events`, exportável por período:

```bash
curl "$BASE/reports/audit-export?from=2026-01-01T00:00:00&to=2026-12-31T00:00:00"
```

## Estrutura

```
src/
  MSRFinancialEngine.Domain           entidades e enums
  MSRFinancialEngine.Application      importadores, matching, workflow, auditoria, relatórios
  MSRFinancialEngine.Infrastructure   EF Core, DbContext, repositórios, migrations
  MSRFinancialEngine.Api              controllers, DI, Swagger
tests/
  MSRFinancialEngine.Tests            testes unitários e de integração
```

Dependências apontam para dentro: `Api → Infrastructure → Application → Domain`.
O `Domain` não depende de nada, e o `Application` não conhece EF Core (acessa dados
por `IRepository<T>`).
