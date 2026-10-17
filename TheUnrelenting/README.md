# The Unrelenting v0.5.0 — Silent Steps (teste experimental)

Mod para WorldBox 0.51.2 build 719 + NeoModLoader no Ubuntu.

## Aviso: use somente um mundo de teste descartavel

Esta versao modifica temporariamente traits de criaturas **que ja existem**.
O mod TENTA remover suas proprias traits ao encerrar a perseguicao, mas **nao ha
persistencia/limpeza garantida depois de salvar, fechar e recarregar o mundo**.
NÃO TESTE em um save importante. O uso de traits pode causar efeitos secundarios
no proprio jogo (traits opostas, alteracoes de comportamento etc.).

## O que faz

- Preserva a trait Haunted e seu identificador (`ArvakTheUnrelenting_Haunted`).
- Seleciona perseguidores entre criaturas vivas do mapa.
- Evita candidatos com Fast, Agile e Plague para reduzir perda acidental de traits
  opostas ao aplicar Slow e Immortal (nao e garantia completa).
- Testa aplicar as traits nativas `slow`, `peaceful`, `immortal`, `super_health`.
  Se um identificador/API falhar, loga falha sem impedir a perseguicao.
- Guarda quais dessas traits NAO existiam e foram de fato adicionadas, para
  tentar removê-las quando o alvo deixa de estar Haunted.
- Continua usando `goTo` e distancia a cada 2 segundos, sem causar danos.
- Corrige log duplicado de 'ALCANCOU O ALVO': agora aparece no maximo uma vez
  por associacao alvo/perseguidor.
- Registra possivel causa de `HAUNTED DEIXOU DE ESTAR ATIVO` como
  `TRAIT_REMOVIDA`, `MORTE_OU_DESAPARECIMENTO`, `SEM_TILE` ou `FORA_DA_LISTA`.

## Limites

- `Peaceful` impede iniciar ataques, MAS nao torna amigavel para todo o mundo;
  outros ainda podem atacar. `Immortal` nao impede morte em combate.
- `super_health` aumenta a vida, NAO garante invulnerabilidade.
- Os modificadores sao visiveis na ficha, entao NAO ha disfarce perfeito ainda.
- Nao existe execucao por proximidade, travessia de oceano garantida nem
  restauracao segura depois de restart/reload. Nao e ainda uma release final.

## Instalacao

1. Feche o WorldBox.
2. Na pasta `worldbox/Mods/`, remova SOMENTE a pasta antiga `TheUnrelenting`.
3. Extraia este ZIP e mova a nova pasta `TheUnrelenting` para `worldbox/Mods/`.
4. Reinicie o WorldBox. Abra um mundo novo descartavel.

## Teste

1. Coloque 10 a 20 unidades em uma ilha e aplique Haunted a UMA delas.
2. Observe se o perseguidor caminha mais lentamente que antes. Ele pode ainda
   ser atacado (isso e esperado no experimento).
3. Remova Haunted e aguarde alguns segundos. O mod deve registrar LIMPEZA TRAIT.
4. No terminal:

```bash
grep -nE 'THE UNRELENTING:|error CS[0-9]+|Failed to compile' \
  "$HOME/.config/unity3d/mkarpenko/WorldBox/Player.log" | tail -n 100
```

Procure por `TRAIT TEMPORARIA APLICADA`, `NAO FOI POSSIVEL APLICAR TRAIT`,
`PERSEGUIDOR ESCOLHIDO`, `PROGRESSO PERSEGUICAO`, `ALCANCOU O ALVO`,
`HAUNTED DEIXOU` e `LIMPEZA TRAIT`.

**Cuidado:** nao confunda ter registrado uma tentativa de aplicar trait com
observar seu efeito. A propria execucao no jogo ainda precisa ser validada.

## GitHub

- Substituir os arquivos anteriores pelos arquivos da nova pasta.
- Commit: `feat: test native pursuer traits and improve pursuit lifecycle logs`
- Tag sugerida apos teste: `v0.5.0`
- Release publica: somente depois de testar com sucesso e registrar limites.

## API e fontes

- https://lous12.github.io/worldbox-modding-docs/api/runtime-actor/
- https://the-official-worldbox-wiki.fandom.com/wiki/Creature_Traits
- https://github.com/WorldBoxOpenMods/ModLoader
