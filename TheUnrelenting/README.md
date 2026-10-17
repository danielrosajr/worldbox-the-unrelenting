# The Unrelenting v0.11.4 — Haunted: Trauma

## Novidade: duas traits, uma mesma entidade

A **v0.11.4** permite identificar a origem da maldicao na ficha da criatura:

| Trait | Como e aplicada | Perseguicao |
|---|---|---|
| **Haunted** (`ArvakTheUnrelenting_Haunted`) | Manual, Origins aleatorio e transferencia ao assassino provavel (The Witness) | Normal |
| **Haunted: Trauma** (`ArvakTheUnrelenting_HauntedTrauma`) | Contagio por testemunhar morte provavelmente violenta, quando Trauma Witness estiver ativo | Exatamente a mesma IA, fila, troca de hospedeiro, arrancadas, Anti-Kite e execucao |

- As duas traits pertencem ao mesmo grupo e sao tratadas como *maldicoes* no codigo.
- **Haunted: Trauma** nao e selecionavel manualmente no editor de traits: apenas o sistema de trauma a aplica. Ela continua aparecendo na ficha do ator afetado.
- A entidade nao abre um segundo perseguidor por haver dois tipos; ambas entram na **mesma fila**.
- Para origens automaticas, o mod usa **Haunted** normal. Para trauma, aplica exclusivamente **Haunted: Trauma**.
- Nao adiciona uma segunda trait a alguem que ja estiver amaldiçoado por qualquer uma delas.
- Preserva o ID da trait Haunted original para nao perder compatibilidade com saves.
- Quem ja recebeu a trait normal por trauma em um mundo criado nas versoes anteriores **nao e convertido retroativamente**: esta versao marca novas transmissoes.
- Se uma pessoa morrer com qualquer variante, o sistema The Witness pode acompanhar a morte (desde que habilitado). A transferencia ao assassino usa Haunted normal; isso identifica o **novo mecanismo de infeccao**, nao uma heranca automatica do nome anterior.
- Nao ha diagnostico separado no jogo: a trait aparece na ficha e o tipo tambem consta no Player.log como `TIPO=TRAUMA` ou `TIPO=NORMAL`.
- Trauma Witness continua **desligado por padrao** para nao infectar populacoes involuntariamente.

### Teste recomendado

1. Em um mundo descartavel, desligue Origins automatico e The Witness > transferencia ao assassino.
2. Ative TraumaWitness > Detectar mortes violentas e Transmitir Haunted por trauma.
3. Configure chance para 100% e cooldown para 10 segundos.
4. Faca um combatente matar outro proximo de uma testemunha humanoide (sem Haunted).
5. Inspecione a ficha da testemunha: deve mostrar **Haunted: Trauma**, nao Haunted comum.
6. Confira a perseguicao e o log:

```bash
grep -nE 'TRAIT HAUNTED: TRAUMA|TRAUMA TRANSMISSAO|HAUNTED ENTROU NA FILA|ALVO DEFINIDO|error CS|Failed to compile' ~/.config/unity3d/mkarpenko/WorldBox/Player.log | tail -n 100
```

Para confirmar a variante normal, marque manualmente outra unidade com **Haunted** e veja `TIPO=NORMAL` no log.

**Limite:** nao compilado nem executado no WorldBox aqui; teste a v0.11.4 antes de usar saves importantes. Para rollback, restaure a pasta completa da v0.11.3. Remova a nova trait dos saves antes de voltar a uma versao anterior.

**GitHub:** `feat: distinguish trauma-origin curses with Haunted Trauma trait`

---


Mod de terror para WorldBox 0.51.2 (build 719) usando NeoModLoader.

## Novo nesta versao

O perseguidor pune tentativas de correr em circulos ao redor dele.

- Mecanica **No More Games**, habilitada por padrao, mas configuravel no menu do NML.
- O alvo deve ficar a ate **15 tiles** por **30 segundos reais** perto do mesmo hospedeiro.
- Para evitar falsos positivos, o mod tambem exige que a vitima tenha **andado ao menos 12 tiles nos ultimos 12 segundos** e tenha **baixo deslocamento liquido** (deslocamento <= 78% do caminho percorrido). Isso identifica circulos/ziguezagues, nao apenas ficar parado.
- Ao detectar provocacao, recebe a trait temporaria `ArvakTheUnrelenting_PunishingRush`, com **+75 no atributo speed**, por **6 segundos reais** (configuravel).
- Ao fim, o mod tenta restaurar a velocidade anterior e so pode disparar novamente depois de **90 segundos reais** (configuravel).
- Sair da zona por mais de 2 segundos reinicia a contagem. Trocar de hospedeiro ou vitima tambem reinicia. Trocas normais de corpo sao adiadas temporariamente se a vitima estiver perto, para permitir chegar aos 30s; resgate por travamento continua habilitado.
- O boost e cancelado/limpo ao abandonar o corpo ou terminar a cadeia.
- **Nao e teleporte**, apenas aumenta velocidade. Navegacao e obstaculos dependem da IA do WorldBox.
- A trait privada pode aparecer temporariamente na ficha durante a arrancada; ela nao aparece no editor de traits. Remocao e testada no `Player.log`.

## Instalacao (Linux)

1. Feche o jogo.
2. Substitua a pasta `TheUnrelenting` na pasta `Mods` do NeoModLoader pelo conteudo do ZIP.
3. Entre no mundo descartavel. Selecione Mods > The Unrelenting > Configuracao > `AntiKite`.
4. Para testar rapidamente, ajuste `Tempo proximo necessario` para 8 segundos e deixe o raio em 15 tiles.
5. Aplique Haunted em uma unidade controlavel e mova-a repetidamente em circulos na frente do perseguidor. Ele precisa permanecer proximo **e a unidade realmente se mover em ciclos**.
6. Cheque os logs:

```bash
grep -nE 'ANTI-KITE|JUMPSCARE|error CS|Failed to compile' ~/.config/unity3d/mkarpenko/WorldBox/Player.log | tail -n 100
```

Procure `ANTI-KITE PROVOCACAO DETECTADA`, `ANTI-KITE JUMPSCARE INICIADO` e `ANTI-KITE JUMPSCARE ENCERRADO`.
Para logs de movimento durante o teste, ligue `VerboseAntiKiteLogs` no menu (deixe desligado no jogo normal).

**Atencao**: experimento ainda nao testado dentro do WorldBox. A sintaxe foi baseada no padrao do mod que funcionou nas versoes anteriores e no registro de traits com `base_stats["speed"]` do guia de modding. Teste apenas em mundo descartavel.

## Continuam presentes

- Trait Haunted, Origins automatico opcional e recorrente
- Selecao da entidade, body-hopping e fila de vitimas
- Speed modes slow/normal/fast e arrancadas normais aleatorias
- AI adaptativa e resgate de hospedeiro travado
- Witness: identificacao conservadora e transmissao opcional ao assassino provavel

## GitHub

Substitua `Main.cs`, `mod.json`, `default_config.json`, `Locales/*`, `README.md` e `LEIA-ME.txt`.

Commit: `feat: punish circling the Haunted pursuer with a supernatural sprint`

Tag: `v0.11.3` (release somente apos teste).


## v0.11.3 — Trauma Witness (experimental)

Trauma Witness is a NEW independent NML configuration group. Tracking is on and transmission is **OFF by default**. Previous Origins, The Witness (probable killer), AntiKite and pursuit behavior remain in place.

A probable violent death is observed when (1) a living actor from the previous 2-second snapshot has now died and exactly one nearby survivor has gained one kill, or (2) the dying actor had a **runtime-confirmed burning status** in the previous scan. No death with zero evidence qualifies. These are **heuristics**, not a reliable event API, visual raycast or guaranteed attribution. The death of an ordinary person can begin the curse only when `IncludeNonHauntedDeaths` is enabled (default: yes). Deaths performed by this mod itself are explicitly excluded. The previously existing killer-transmission mechanic has priority; a death already passed to the killer will not infect an additional witness.

Only humanoid witnesses (`human`, `elf`, `dwarf`, `orc`) that are alive, not the current host, not the likely killer and not Haunted qualify. At most one witness is chosen per death; success is rolled using `TraumaChancePercent` and a small exposure bonus (+5 percentage points per prior exposure, max +15). Global cooldown (default 120 real seconds) activates after a successful trauma transfer. For safety, Trauma tracking pauses if >6500 live actors are present, with a log warning.

### Recommended disposable-world tests

1. Disable automatic Origins, leave `TheWitness.EnableKillerTransfer` off, enable `TraumaWitness.EnableTraumaTransfer` and `VerboseTraumaLogs`.
2. Set trauma chance to 100% and cooldown to 10 seconds. Create victim + one attacker + **one other humanoid within 10 tiles**. The attacker should kill the ordinary victim using normal game combat. You should see `TRAUMA MORTE VIOLENTA PROVAVEL`, `TRAUMA TESTEMUNHA SORTEADA`, `TRAUMA TRANSMISSAO CONFIRMADA` and `HAUNTED ENTROU NA FILA`.
3. Repeat without a nearby bystander: no transfer. Kill by natural aging: no transfer. Keep a second humanoid close by during a large battle: no guarantees in ambiguous cases; check diagnostics.
4. Turn `IncludeNonHauntedDeaths` off and try the same non-Haunted death: no trauma-origin transmission.
5. Return chance/cooldown to defaults and disable test logging. Never test an experimental death-based curse on an important save.

Log command:
```
grep -nE 'TRAUMA|WITNESS TRANSMISSAO|HAUNTED ENTROU|error CS|Failed to compile' ~/.config/unity3d/mkarpenko/WorldBox/Player.log | tail -n 100
```

**Note:** this version does not detect actual on-screen visibility, subjective fear, or reliably infer the cause of death. Some versions may not expose burning status; then only probable homicide is detected. No new UI window: configuration and debug logs are in NML and `Player.log` respectively.

**Rollback:** use v0.11.2 zip and replace the entire mod folder.

**GitHub commit:** `feat: add opt-in Trauma Witness transmission for violent deaths`
