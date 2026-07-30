# Motor de Reconciliação Financeira Multi-Fonte

## Objetivo

Ingerir extratos bancários, faturas, notas fiscais e dados de ERPs diferentes, casar
transações automaticamente (regras determinísticas + fuzzy matching), e sinalizar
divergências para revisão humana. Serve qualquer área financeira/contábil corporativa
que precise reconciliar múltiplas fontes de dados transacionais.

## Stack

- **Runtime**: .NET 8 (ASP.NET Core Web API)
- **Persistência**: PostgreSQL
- **ORM**: EF Core
- **Fila/mensageria** (para ingestão assíncrona): a definir — candidatos: canal interno
  do próprio .NET (Channels) para MVP, RabbitMQ ou Azure Service Bus se precisar de
  distribuição entre serviços
- **Fuzzy matching**: biblioteca própria sobre distância de string (Levenshtein/Jaro-Winkler)
  combinada com regras de tolerância numérica (valor, data)

## Módulos principais

### 1. Ingestão (Import)

Responsável por trazer dados de fontes heterogêneas para um modelo canônico interno.

- Cada fonte (banco X, ERP Y, layout de fatura Z) tem um **Parser** que implementa uma
  interface comum `ISourceImporter` e produz `RawTransaction`.
- Formatos: CSV, OFX, MT940, XML (NF-e), APIs REC (ERP).
- Adicionar uma nova fonte = implementar um novo parser + registrar no catálogo de
  importadores. Não deve exigir mudança no núcleo de matching.

### 2. Normalização

Transforma `RawTransaction` (formato específico da fonte) em `CanonicalTransaction`
(modelo interno único): valor, moeda, data, descrição normalizada, empresa, conta,
documento de referência (se houver), fonte de origem, hash de idempotência.

### 3. Matching Engine

- **Regras determinísticas**: comparação exata por chave (nº documento, valor + data +
  conta). Rodam primeiro, são baratas e não ambíguas.
- **Fuzzy matching**: para o que sobra, usa distância textual na descrição + tolerância
  de valor/data configurável, gerando um *score* de confiança.
- Resultado de cada tentativa de match vira um `MatchCandidate` com score e a decisão
  (auto-aprovado acima de um limiar, ou enviado para revisão).
- Motor de regras deve ser extensível sem recompilar o núcleo — considerar regras como
  dados configuráveis (tabela de regras) versus regras como código (estratégias
  plugáveis). Recomendo: regras determinísticas simples como dados configuráveis;
  lógica de scoring fuzzy como estratégias em código, versionadas.

### 4. Divergências e Workflow de Aprovação

- Toda transação não conciliada automaticamente vira uma `Divergence` com motivo
  (sem par, múltiplos candidatos, valor fora de tolerância, moeda incompatível, etc.).
- Workflow de aprovação: fila de revisão, atribuição a usuário/time, decisão
  (aceitar sugestão, match manual, marcar como não-reconciliável), auditoria de quem
  decidiu e quando.
- Este módulo cresce naturalmente para suportar múltiplos níveis de aprovação por
  valor ou por empresa.

### 5. Auditoria e Relatórios

- Toda ação relevante (import, match automático, decisão manual, edição de regra)
  gera um evento de auditoria imutável.
- Relatórios: taxa de conciliação automática, divergências em aberto por idade,
  histórico de decisões por usuário, exportação para auditoria externa.

### 6. Multi-moeda e Multi-empresa

- `CanonicalTransaction` carrega `CompanyId` e `CurrencyCode` desde o início — não
  tratar como add-on posterior, pois retrofit em cima do schema de matching é caro.
- Conversão de câmbio: taxa de referência por data, armazenada para permitir
  reconciliação auditável (não recalcular retroativamente com taxa atual).
- Isolamento de dados por empresa: via `CompanyId` em todas as tabelas + filtro
  obrigatório na camada de acesso a dados (não depender de query manual em cada lugar).

## Modelo de dados (alto nível)

```
Company (Id, Name, ...)
Source (Id, Type, CompanyId, ConfigJson)
RawTransaction (Id, SourceId, PayloadJson, ImportedAt)
CanonicalTransaction (Id, CompanyId, SourceId, Amount, CurrencyCode,
                       TransactionDate, Description, ReferenceDoc, Hash)
MatchingRule (Id, CompanyId, Type[Deterministic|Fuzzy], ConfigJson, Priority, Active)
MatchCandidate (Id, TransactionAId, TransactionBId, Score, RuleId, Status)
Divergence (Id, TransactionId, Reason, Status, AssignedTo)
ApprovalDecision (Id, DivergenceId, UserId, Decision, DecidedAt, Notes)
AuditEvent (Id, EntityType, EntityId, Action, UserId, Timestamp, DetailsJson)
ExchangeRate (CurrencyCode, Date, RateToBase)
```

## Ordem de expansão sugerida (MVP → crescimento)

1. **MVP**: 1 fonte de importação (ex: CSV de extrato bancário) + matching
   determinístico simples (valor + data) + fila de divergências manual, single-company,
   single-currency.
2. Adicionar segunda fonte (ERP) + fuzzy matching por descrição.
3. Workflow de aprovação com múltiplos usuários/papéis.
4. Multi-moeda (câmbio histórico).
5. Multi-empresa (isolamento de dados, papéis por empresa).
6. Relatórios de auditoria e exportação.

Cada etapa deve ser entregável e testável isoladamente — o núcleo (normalização +
matching) não deve precisar de retrabalho estrutural ao adicionar itens 2-6, só
extensão pelos pontos definidos acima (novo parser, nova regra, novo relatório).

## Pontos em aberto para decidir depois

- Mensageria para ingestão assíncrona (Channels in-process vs. fila externa).
- Onde hospedar `ConfigJson` de regras: banco vs. arquivo de configuração versionado.
- Estratégia de reprocessamento: o que acontece se um parser mudar e dados já
  importados precisarem ser re-normalizados.
