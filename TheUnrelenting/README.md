# The Unrelenting v0.6.0 — First Contact (experimental)

Mod para WorldBox **0.51.2 build 719**, usando NeoModLoader no Ubuntu.

> **AVISO: TESTAR SOMENTE NUM MUNDO DESCARTAVEL.** Esta versao pode eliminar tanto
> a vitima quanto uma criatura normal selecionada como perseguidor. Nao teste em
> saves importantes. As traits nativas podem causar efeitos colaterais e sua
> limpeza nao e garantida apos salvar/recarregar.

## O que mudou desde a v0.5.0

- Preserva a trait `Haunted` e ID `ArvakTheUnrelenting_Haunted`.
- Preserva a escolha secreta de um perseguidor existente e a perseguicao via `goTo`.
- Aplica temporariamente `slow`, `peaceful`, `immortal` e `super_health` quando faltantes.
- Quando o perseguidor chega a **1.5 tiles** do alvo, chama `Actor.dieSimpleNone()`
  para a vitima (execucao direta sem dano em area).
- Confirma no log se a vitima morreu. Somente se isso for confirmado, limpa
  traits do perseguidor, remove-o com `dieSimpleNone()` e encerra o vinculo.
- Evita executar o mesmo vinculo repetidamente e nao continua emitindo
  ordens de movimento a um alvo executado.
- Ainda identifica unidades apenas durante a sessao atual: **sem persistencia de
  associacoes apos reiniciar ou recarregar save**.

## O que AINDA nao esta pronto

- `Peaceful` nao impede que outros ataquem o perseguidor: neutralidade total
  ainda NAO foi implementada.
- `Immortal` nao evita ataques, e `super_health` nao garante invulnerabilidade.
- Traits do perseguidor ficam visiveis na ficha: o disfarce NAO e perfeito.
- Travessia de oceanos, estabilidade sob pause/reload, obstaculos e selecao de
  candidatos do mesmo continente ainda nao foram resolvidos.
- O perseguidor pode ser uma unidade comum e sua morte pode afetar o mundo.

## Instalacao

1. Feche o WorldBox.
2. Na pasta `worldbox/Mods/`, remova apenas a pasta antiga `TheUnrelenting`.
3. Extraia o ZIP e mova sua pasta `TheUnrelenting` para `worldbox/Mods/`.
4. Abra WorldBox e CRIE UM MUNDO NOVO DESCARTAVEL.

## Teste controlado

1. Crie uma ilha conectada com de 10 a 20 unidades comuns.
2. Aplique a trait `Haunted` a apenas uma criatura.
3. Deixe o mundo rodar e observe as duas unidades se aproximando.
4. Quando o perseguidor encostar, verifique se a vitima e o perseguidor
   desapareceram/morreram. **Nao salve esse mundo.**
5. Rode no Ubuntu:

```bash
grep -nE 'THE UNRELENTING:|error CS[0-9]+|Failed to compile' \
  "$HOME/.config/unity3d/mkarpenko/WorldBox/Player.log" | tail -n 120
```

Esperado em caso de sucesso:

```
THE UNRELENTING: CONTATO COM A VITIMA!
THE UNRELENTING: EXECUCAO CONFIRMADA!
THE UNRELENTING: PERSEGUIDOR ENCERRADO! ... MORTO=True
```

Se ocorrer `FALHA NA EXECUCAO`, `EXECUCAO SEM CONFIRMACAO` ou
`FALHA AO ENCERRAR PERSEGUIDOR`, envie o log.

## Atualizacao do GitHub

- Substituir `Main.cs`, `mod.json`, `README.md`, `LEIA-ME.txt` e `Locales/`.
- Commit: `feat: add lethal Haunted encounter and pursuer cleanup`
- Versao: `v0.6.0` (experimental; release somente apos teste).

## Fontes e compatibilidade

- https://lous12.github.io/worldbox-modding-docs/api/runtime-entity-lifecycle/
- https://lous12.github.io/worldbox-modding-docs/api/runtime-actor/
- https://github.com/WorldBoxOpenMods/ModLoader
