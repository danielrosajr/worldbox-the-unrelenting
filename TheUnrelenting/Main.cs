using NeoModLoader.api;

namespace Arvak.TheUnrelenting
{
    public class Main : BasicMod<Main>
    {
        // IDs permanentes: nao mudar para manter compatibilidade com futuros saves.
        public const string TraitId = "ArvakTheUnrelenting_Haunted";
        public const string GroupId = "ArvakTheUnrelenting_Group";

        protected override void OnModLoad()
        {
            LogInfo("THE UNRELENTING: MOD CARREGADO COM SUCESSO!");

            // Na build 719 do WorldBox, a cor do grupo precisa ser uma string.
            ActorTraitGroupAsset group = new ActorTraitGroupAsset()
            {
                id = GroupId,
                name = "trait_group_ArvakTheUnrelenting",
                color = "#9755C7"
            };
            AssetManager.trait_groups.add(group);

            // Trait manual, sem bonus, comportamento ou perseguidor por enquanto.
            // birth e PlayerConfig.unlockTrait nao existem no WorldBox 0.51.2 (719).
            ActorTrait haunted = new ActorTrait()
            {
                id = TraitId,
                group_id = GroupId,
                path_icon = "ui/icons/neomodloader",
                can_be_given = true,
                can_be_removed = true,
                can_be_cured = false,
                needs_to_be_explored = false
            };
            AssetManager.traits.add(haunted);

            LogInfo("THE UNRELENTING: TRAIT HAUNTED REGISTRADA COM SUCESSO! ID=" + TraitId);
        }
    }
}
