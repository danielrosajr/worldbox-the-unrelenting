# The Unrelenting v0.4.0 — The Pursuit (experimental)

Mod experimental de terror para **WorldBox 0.51.2 build 719** e **NeoModLoader** no Ubuntu.

A v0.3.0 foi confirmada no jogo pelo autor. **A v0.4.0 ainda precisa ser testada no WorldBox.**

## O que faz

- Mantem trait `Haunted`, seu ID `ArvakTheUnrelenting_Haunted`, GUID e grupo da v0.3.0.
- Identifica vitimas a cada ~2 segundos reais e atribui a cada uma um perseguidor aleatorio ja existente.
- Prefere unidades a ate 55 tiles de distancia; se nao houver, considera criaturas mais distantes (que podem nao encontrar rota).
- Ao escolher, cancela a rotina atual do perseguidor UMA vez e manda seguir a posicao atual da vitima por `goTo(target.current_tile, false)` a cada ~2 segundos.
- Loga a distancia na selecao, progresso a cada ~15 segundos reais e quando o perseguidor se aproxima a ~1,5 tiles.
- Se Haunted for removido ou o alvo desaparecer, interrompe as novas ordens de perseguicao.
- Se o perseguidor morrer ou ficar Haunted, pode atribuir outro.

**Ainda NAO ha**: execucao da vitima, imortalidade, neutralidade completa, lentidao, travessia segura de oceanos, persistencia de pares entre saves, garantia de monopolizar a IA normal. O movimento e experimental; mares e obstaculos podem impedir o trajeto.

## Instalacao

1. Feche WorldBox.
2. Remova a pasta antiga `TheUnrelenting` de `worldbox/Mods` (apenas essa pasta).
3. Extraia este ZIP e coloque a nova pasta `TheUnrelenting` no mesmo lugar.
4. Abra WorldBox e confirme The Unrelenting no menu NML.

## Teste controlado

1. Crie um mapa com terra continua e 10-20 criaturas em uma area relativamente pequena (reduz falhas de caminho).
2. Aplique `Haunted` em 1 criatura.
3. Deixe o jogo rodando durante 30-60 segundos reais.
4. Execute:

```bash
grep -nE 'THE UNRELENTING:|error CS[0-9]+|Failed to compile' \
  "$HOME/.config/unity3d/mkarpenko/WorldBox/Player.log" | tail -n 80
```

As mensagens `PERSEGUIDOR ESCOLHIDO`, `PROGRESSO PERSEGUICAO` e possivelmente `PERSEGUIDOR ALCANCOU O ALVO (SEM MATAR)` indicam progresso.

**Verificacao critica:** em `PROGRESSO PERSEGUICAO` veja se `DISTANCIA` ficou MENOR que `DISTANCIA_ANTERIOR`. O fato de `goTo` ter sido invocado nao garante deslocamento bem sucedido. Veja tambem se o mundo esta pausado e se o jogo nao esta aguardando loading.

Se um erro ocorrer, envie o arquivo de erro do NeoModLoader ou as ultimas 80 linhas relevantes do Player.log.

## GitHub

- Substituir `Main.cs`, `mod.json`, `README.md`, `LEIA-ME.txt` e `Locales/*.json` pelos arquivos deste ZIP.
- Commit: `feat: add experimental Haunted pursuer movement`
- Versao: `v0.4.0` (release so apos teste bem-sucedido).

## Fontes e referencia da API

- https://github.com/studiocadogan/SemiAutoActions/blob/main/SemiAutoActionSystem.cs (`Actor.goTo(WorldTile, false)` e `cancelAllBeh()`)
- https://lous12.github.io/worldbox-modding-docs/quick/actors/ (`distanceToActorTile`)
- https://github.com/WorldBoxOpenMods/ModLoader
