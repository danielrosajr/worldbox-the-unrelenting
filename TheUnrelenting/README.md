# The Unrelenting — v0.1.1 (Haunted)

Mod experimental de terror para **WorldBox 0.51.2 (build 719)** com **NeoModLoader**.

## Correcoes v0.1.1

O arquivo de erro enviado pelo usuario apontou tres erros de compilacao na v0.1.0:

- `ActorTraitGroupAsset.color` exige `string`, nao `UnityEngine.Color`.
- `ActorTrait.birth` nao existe na versao instalada.
- `PlayerConfig.unlockTrait` nao existe na versao instalada.

Estas tres referencias foram corrigidas/removidas. **O carregamento in-game desta versao ainda precisa ser validado pelo usuario.**

## Funcionalidade desta versao

- Registra uma trait manual **Haunted** (ID `ArvakTheUnrelenting_Haunted`).
- Cria o grupo de traits **The Unrelenting**.
- Trait pode ser aplicada e removida e nao modifica atributos.
- Icone temporario: NeoModLoader.
- Ainda **nao** existe perseguidor nem morte automatica.

## Instalacao (Steam nativa, Linux)

1. Feche o WorldBox.
2. Abra a pasta `Mods` na **raiz** do WorldBox, a mesma em que a v0.0.1 funcionou.
3. **Apague a pasta antiga `TheUnrelenting` dentro de `Mods`** (nao apague outros mods).
4. Extraia o ZIP e coloque a **nova** pasta `TheUnrelenting` ali. Nao deixe duas copias do mesmo mod.
5. Abra o WorldBox.
6. Confirme que The Unrelenting aparece no NeoModLoader e procure **Haunted** no editor de traits.
7. Aplique Haunted em um habitante e veja se aparece na ficha; depois remova.

## Diagnostico

Execute apos iniciar o jogo:

```bash
grep -nE 'THE UNRELENTING:|error CS[0-9]+|Failed to compile mod The Unrelenting|Exception'   "$HOME/.config/unity3d/mkarpenko/WorldBox/Player.log" | tail -n 50
```

Sinais esperados:
- `THE UNRELENTING: MOD CARREGADO COM SUCESSO!`
- `THE UNRELENTING: TRAIT HAUNTED REGISTRADA COM SUCESSO!`

Se apenas o primeiro aparecer ou houver um erro novo, compartilhe o log gerado pelo NeoModLoader.

## GitHub

Substitua no repositorio os arquivos da versao anterior, mantendo o diretorio `Locales`.

Commit sugerido:

`fix: restore Haunted trait compatibility with WorldBox 0.51.2`

Versao em `mod.json`: `0.1.1`. Crie release **somente apos testar**.

## Referencia de API

- https://github.com/WorldBoxOpenMods/ModExample/blob/master/content/ExampleTraits.cs (tem exemplos de API antiga)
- https://github.com/Gear-jake/ClassicalEconomics/blob/main/EconomyModMain.cs (exemplo de cor hexadecimal / sem unlockTrait)
