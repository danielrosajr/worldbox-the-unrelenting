# The Unrelenting — v0.1.0

Mod de horror para WorldBox com NeoModLoader. Desenvolvimento incremental no Linux/Ubuntu.

## Funcionalidade desta versão

- Registra uma trait personalizada **Amaldiçoado** / **Cursed**.
- Cria o grupo **The Unrelenting** no editor de traits.
- Permite aplicar/remover manualmente a trait.
- A trait ainda não aplica modificadores, não invoca entidades nem inicia perseguições.
- O ícone é o ícone padrão do NeoModLoader, temporariamente.

## Atualização a partir da v0.0.1

1. Feche o WorldBox.
2. Faça cópia de segurança da sua pasta `TheUnrelenting` atual, se você alterou algo nela.
3. Na pasta `Mods` usada pelo NeoModLoader (a mesma em que v0.0.1 funcionou), **substitua** `TheUnrelenting` pelo diretório incluído neste ZIP.
4. Não instale a versão antiga e a nova lado a lado: ambas têm o mesmo GUID.
5. Inicie o jogo e abra o editor de traits. Procure o grupo `The Unrelenting` e aplique `Amaldiçoado` a uma criatura.

## Verificando o log no Ubuntu

```bash
find "$HOME/.config/unity3d" -name Player.log -exec grep -HnE "THE UNRELENTING:|error CS[0-9]+|Exception" {} +
```

A linha de confirmação esperada é:

```
THE UNRELENTING: TRAIT AMALDICOADO REGISTRADA COM SUCESSO!
```

**Se o mod carregar, mas a trait não aparecer:** reinicie o WorldBox, confira que existe apenas uma pasta do mod e investigue `Player.log`. Dependendo do idioma do jogo, a trait poderá aparecer como **Cursed** ou **Amaldiçoado**. O ícone provisório será o do NeoModLoader.

## Dados técnicos

- GUID imutável: `Arvak.TheUnrelenting`
- ID da trait: `ArvakTheUnrelenting_Cursed`
- ID do grupo: `ArvakTheUnrelenting_Group`
- Localização: `Locales/en.json`, `Locales/pt_BR.json`
- Código-fonte em `Main.cs`; o NeoModLoader compila durante o carregamento.

## Limitações e próximos passos

Esta versão foi preparada com base na API pública e nos exemplos do NeoModLoader, mas não foi executada no seu WorldBox. O teste final é dentro do seu jogo. A próxima versão registrará quando a trait é aplicada a uma unidade; em seguida, selecionaremos o perseguidor.

Referência: https://github.com/WorldBoxOpenMods/ModExample/blob/master/content/ExampleTraits.cs
