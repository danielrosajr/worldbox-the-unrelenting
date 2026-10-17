using System;
using System.Collections.Generic;
using NeoModLoader.api;
using UnityEngine;

namespace Arvak.TheUnrelenting
{
    public class Main : BasicMod<Main>
    {
        // Keep these IDs stable to avoid breaking worlds with Haunted already applied.
        public const string TraitId = "ArvakTheUnrelenting_Haunted";
        public const string GroupId = "ArvakTheUnrelenting_Group";

        // In-memory references: this is only a live-world prototype.
        // Real persistence across saves will be a later milestone.
        private HashSet<Actor> _hauntedUnits = new HashSet<Actor>();
        private float _scanClock = 0f;
        private bool _reportedScanError = false;

        protected override void OnModLoad()
        {
            LogInfo("THE UNRELENTING: MOD CARREGADO COM SUCESSO!");

            ActorTraitGroupAsset group = new ActorTraitGroupAsset()
            {
                id = GroupId,
                name = "trait_group_ArvakTheUnrelenting",
                color = "#9755C7"
            };
            AssetManager.trait_groups.add(group);

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
            LogInfo("THE UNRELENTING: DETECTOR HAUNTED ATIVO (verificacao a cada 2 segundos).");
        }

        // Called by Unity. We deliberately scan slowly to limit work in large worlds.
        private void Update()
        {
            _scanClock += Time.unscaledDeltaTime;
            if (_scanClock < 2f)
                return;
            _scanClock = 0f;

            if (World.world == null)
            {
                // A previously loaded world may have been closed or replaced.
                _hauntedUnits.Clear();
                return;
            }

            try
            {
                HashSet<Actor> current = new HashSet<Actor>();

                foreach (Actor actor in World.world.units)
                {
                    if (actor == null || !actor.isAlive() || !actor.hasTrait(TraitId))
                        continue;

                    current.Add(actor);
                    if (!_hauntedUnits.Contains(actor))
                    {
                        // TEMP_ID helps distinguish units in this debug build.
                        // It is NOT a save-stable identifier.
                        LogInfo("THE UNRELENTING: HAUNTED DETECTADO! TEMP_ID=" + actor.GetHashCode());
                    }
                }

                foreach (Actor oldActor in _hauntedUnits)
                {
                    if (!current.Contains(oldActor))
                    {
                        LogInfo("THE UNRELENTING: HAUNTED DEIXOU DE ESTAR ATIVO! TEMP_ID=" + oldActor.GetHashCode());
                    }
                }

                _hauntedUnits = current;
                _reportedScanError = false;
            }
            catch (Exception error)
            {
                // During world changes a scan may fail temporarily. Log once,
                // not every two seconds, so the player log remains readable.
                if (!_reportedScanError)
                {
                    LogWarning("THE UNRELENTING: ERRO AO PROCURAR HAUNTED: " + error);
                    _reportedScanError = true;
                }
            }
        }
    }
}
