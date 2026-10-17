using NeoModLoader.api;
using UnityEngine;

namespace Arvak.TheUnrelenting
{
    public class Main : BasicMod<Main>
    {
        // Identificadores estaveis: evitar renomear em versoes futuras.
        public const string TraitId = "ArvakTheUnrelenting_Cursed";
        public const string GroupId = "ArvakTheUnrelenting_Group";

        protected override void OnModLoad()
        {
            // Nosso marcador da v0.0.1 continua existindo.
            LogInfo("THE UNRELENTING: MOD CARREGADO COM SUCESSO!");

            // Grupo proprio na lista de traits.
            // A API segue o exemplo oficial do projeto ModExample do NeoModLoader.
            ActorTraitGroupAsset group = new ActorTraitGroupAsset()
            {
                id = GroupId,
                name = "trait_group_ArvakTheUnrelenting",
                color = Color.magenta
            };
            AssetManager.trait_groups.add(group);

            // A trait e uma marca: nao muda atributos e nao nasce naturalmente.
            // A perseguicao sera implementada em uma versao posterior.
            ActorTrait cursed = new ActorTrait()
            {
                id = TraitId,
                group_id = GroupId,
                path_icon = "ui/icons/neomodloader", // Icone provisoria do loader.
                birth = 0f,
                can_be_given = true,
                can_be_removed = true,
                can_be_cured = false,
                needs_to_be_explored = false
            };
            AssetManager.traits.add(cursed);

            // Torna a trait selecionavel no editor sem exigir descoberta previa.
            PlayerConfig.unlockTrait(TraitId);

            LogInfo("THE UNRELENTING: TRAIT AMALDICOADO REGISTRADA COM SUCESSO! ID=" + TraitId);
        }
    }
}
