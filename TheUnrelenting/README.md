# The Unrelenting — v0.1.0 (Haunted)

Mod de terror para WorldBox com NeoModLoader.

## O que esta versão faz
- Registra a trait **Haunted** (identificador interno `ArvakTheUnrelenting_Haunted`).
- Cria o grupo **The Unrelenting** no editor de traits.
- A trait pode ser aplicada e removida pelo jogador, sem alterar atributos.
- Ainda não invoca nenhum perseguidor.
- Usa provisoriamente o ícone do NeoModLoader.

## Atualização
1. Feche o WorldBox.
2. Vá à pasta `Mods` usada pelo NeoModLoader (aquela onde v0.0.1 funcionou).
3. Substitua completamente a pasta `TheUnrelenting` pela pasta deste ZIP. Não deixe duas versões com o mesmo GUID.
4. Abra o WorldBox, entre num mundo, selecione uma criatura e abra o **Trait Editor** na ficha da unidade.
5. Procure a trait **Haunted** (possivelmente dentro do grupo **The Unrelenting**). Adicione à criatura; verifique se aparece na lista de traits.
6. Remova a trait e confirme que desaparece.

## Verificação no Ubuntu

```bash
find "$HOME/.config/unity3d" -name Player.log -exec grep -HnE 'THE UNRELENTING:|error CS[0-9]+|Exception' {} +
```

Log esperado: `THE UNRELENTING: TRAIT HAUNTED REGISTRADA COM SUCESSO!`

**Atenção:** a confirmação no log significa que o registro da trait foi executado; também é necessário confirmar visualmente a trait no editor e na ficha da unidade.

## Detalhes técnicos
- GUID do mod (mantido): `Arvak.TheUnrelenting`
- Trait (corrigida): `ArvakTheUnrelenting_Haunted`
- Grupo: `ArvakTheUnrelenting_Group`
- Código: `Main.cs`
- Idiomas: `Locales/en.json`, `Locales/pt.json` e `Locales/pt_BR.json` (compatibilidade a verificar no jogo).

## GitHub
- Substituir a versão antiga do código, sem duplicar pastas.
- Commit sugerido: `feat: add Haunted trait and custom trait group`
- Versão: `v0.1.0` (publique release após testar).

## Próximos passos
Identificar unidades com Haunted, escolher um perseguidor e implementar a perseguição lenta com neutralidade e disfarce.

API consultada: https://github.com/WorldBoxOpenMods/ModExample/blob/master/content/ExampleTraits.cs
