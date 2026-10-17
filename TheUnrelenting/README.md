# The Unrelenting v0.11.2 — No More Games

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

Tag: `v0.11.2` (release somente apos teste).
