# The Unrelenting — v0.3.0 (escolha secreta do perseguidor)

Mod experimental de terror para **WorldBox 0.51.2 (build 719)** com **NeoModLoader**, no Ubuntu.
A v0.2.0 (detector Haunted) foi **validada em jogo pelo autor**.
**A v0.3.0 precisa ser testada no seu WorldBox.**

## Novidades da v0.3.0

- Mantem o GUID `Arvak.TheUnrelenting` e a trait `ArvakTheUnrelenting_Haunted` (sem mudar saves anteriores).
- Preserva o detector da v0.2.0: identifica quando Haunted aparece/desaparece a cada ~2 segundos reais.
- Para cada unidade Haunted, sorteia **uma unidade viva ja existente** no mundo, sem Haunted e que nao esteja reservada para outra vitima.
- Registra no log `ALVO_TEMP_ID` e `PERSEGUIDOR_TEMP_ID` para validacao.
- Se o alvo perder Haunted, o vinculo de perseguidor e descartado; se o perseguidor morrer ou ganhar Haunted, outro pode ser sorteado.
- Trocar de mundo limpa os vinculos mantidos em memoria.
- **Nao** altera IA, aparencia, traits, velocidade, resistencia ou dano. O perseguidor **ainda nao persegue nem mata**.
- Sorteio considera todo o mundo; distancia/mesma ilha serao criterios futuros.
- `TEMP_ID` usa `GetHashCode()` exclusivamente para diagnostico e NAO e persistente entre jogos/saves.

## Como instalar

1. Feche o WorldBox.
2. No diretorio `Mods` do NeoModLoader, **substitua somente** a pasta antiga `TheUnrelenting` pela pasta `TheUnrelenting` deste ZIP.
3. Abra o jogo e um mundo com varias criaturas vivas.
4. Aplique a trait `Haunted` a uma ou duas criaturas diferentes. Aguarde ~2 segundos.
5. Consulte o `Player.log`. Depois remova Haunted de uma delas e confira a mensagem de desativacao.

```bash
grep -nE 'THE UNRELENTING:|error CS[0-9]+|Failed to compile' \
  "$HOME/.config/unity3d/mkarpenko/WorldBox/Player.log" | tail -n 50
```

### Logs esperados (exemplo)

```
THE UNRELENTING: SELETOR DE PERSEGUIDORES ATIVO (verificacao a cada 2 segundos).
THE UNRELENTING: HAUNTED DETECTADO! TEMP_ID=2322
THE UNRELENTING: PERSEGUIDOR ESCOLHIDO! ALVO_TEMP_ID=2322 PERSEGUIDOR_TEMP_ID=4567
THE UNRELENTING: HAUNTED DEIXOU DE ESTAR ATIVO! TEMP_ID=2322
```

**Dica:** com uma unica criatura no mapa e Haunted nela, o log avisara que nao existem perseguidores disponiveis. Adicione mais criaturas para testar.

## GitHub

- Substituir `Main.cs`, `mod.json`, `README.md`, `LEIA-ME.txt` e os arquivos `Locales/*.json` existentes. Nao excluir o historico nem trocar GUID/ID da trait.
- Commit: `feat: assign hidden pursuers to Haunted units`
- Versao: `v0.3.0`.
- Release: publique so depois de validar em jogo.

## Referencias

- https://github.com/WorldBoxOpenMods/ModExample
- https://github.com/WorldBoxOpenMods/ModLoaderPullRequests/blob/master/api/BasicMod.cs
