using System;
using System.Collections.Generic;
using NeoModLoader.api;
using UnityEngine;

namespace Arvak.TheUnrelenting
{
    public class Main : BasicMod<Main>
    {
        // Identificadores permanentes: nao mudar entre versoes.
        public const string TraitId = "ArvakTheUnrelenting_Haunted";
        public const string GroupId = "ArvakTheUnrelenting_Group";

        // Apenas referencias em memoria. Nenhum efeito sobre os perseguidores ainda.
        private HashSet<Actor> _hauntedUnits = new HashSet<Actor>();
        private Dictionary<Actor, Actor> _pursuerByTarget = new Dictionary<Actor, Actor>();
        private HashSet<Actor> _noCandidateNotice = new HashSet<Actor>();
        private readonly System.Random _random = new System.Random();
        private object _activeWorldReference;
        private float _scanClock;
        private bool _reportedScanError;

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
            LogInfo("THE UNRELENTING: SELETOR DE PERSEGUIDORES ATIVO (verificacao a cada 2 segundos).");
        }

        private void ResetTracking()
        {
            _hauntedUnits.Clear();
            _pursuerByTarget.Clear();
            _noCandidateNotice.Clear();
        }

        // Escolhe uniformemente entre unidades vivas, sem Haunted, nao reservadas.
        // Reservoir sampling: nao cria uma lista adicional de candidatos.
        private Actor PickPursuer(List<Actor> alive, HashSet<Actor> haunted,
                                  HashSet<Actor> reserved)
        {
            Actor chosen = null;
            int eligible = 0;
            foreach (Actor candidate in alive)
            {
                if (haunted.Contains(candidate) || reserved.Contains(candidate))
                    continue;

                eligible++;
                if (_random.Next(eligible) == 0)
                    chosen = candidate;
            }
            return chosen;
        }

        private void Update()
        {
            _scanClock += Time.unscaledDeltaTime;
            if (_scanClock < 2f)
                return;
            _scanClock = 0f;

            if (World.world == null)
            {
                ResetTracking();
                _activeWorldReference = null;
                return;
            }

            // Evita referencias antigas quando outro mundo e carregado.
            if (!object.ReferenceEquals(_activeWorldReference, World.world))
            {
                ResetTracking();
                _activeWorldReference = World.world;
            }

            try
            {
                HashSet<Actor> current = new HashSet<Actor>();
                List<Actor> alive = new List<Actor>();

                foreach (Actor actor in World.world.units)
                {
                    if (actor == null || !actor.isAlive())
                        continue;

                    alive.Add(actor);
                    if (actor.hasTrait(TraitId))
                    {
                        current.Add(actor);
                        if (!_hauntedUnits.Contains(actor))
                        {
                            LogInfo("THE UNRELENTING: HAUNTED DETECTADO! TEMP_ID=" + actor.GetHashCode());
                        }
                    }
                }

                foreach (Actor oldTarget in _hauntedUnits)
                {
                    if (!current.Contains(oldTarget))
                    {
                        LogInfo("THE UNRELENTING: HAUNTED DEIXOU DE ESTAR ATIVO! TEMP_ID=" + oldTarget.GetHashCode());
                        _pursuerByTarget.Remove(oldTarget);
                        _noCandidateNotice.Remove(oldTarget);
                    }
                }

                // Um perseguidor morto, ou que passou a ter Haunted, nao pode seguir sendo usado.
                List<Actor> invalidTargets = new List<Actor>();
                foreach (KeyValuePair<Actor, Actor> pair in _pursuerByTarget)
                {
                    if (!current.Contains(pair.Key) || pair.Value == null ||
                        !pair.Value.isAlive() || current.Contains(pair.Value))
                    {
                        invalidTargets.Add(pair.Key);
                    }
                }
                foreach (Actor target in invalidTargets)
                {
                    if (current.Contains(target))
                    {
                        LogInfo("THE UNRELENTING: PERSEGUIDOR INDISPONIVEL; NOVA ESCOLHA NECESSARIA. ALVO_TEMP_ID=" + target.GetHashCode());
                    }
                    _pursuerByTarget.Remove(target);
                }

                HashSet<Actor> reserved = new HashSet<Actor>(_pursuerByTarget.Values);
                foreach (Actor target in current)
                {
                    if (_pursuerByTarget.ContainsKey(target))
                        continue;

                    Actor pursuer = PickPursuer(alive, current, reserved);
                    if (pursuer == null)
                    {
                        // Evita repetir o aviso em cada varredura de dois segundos.
                        if (_noCandidateNotice.Add(target))
                        {
                            LogWarning("THE UNRELENTING: NENHUM PERSEGUIDOR DISPONIVEL PARA ALVO_TEMP_ID=" + target.GetHashCode());
                        }
                        continue;
                    }

                    _noCandidateNotice.Remove(target);
                    _pursuerByTarget[target] = pursuer;
                    reserved.Add(pursuer);
                    LogInfo("THE UNRELENTING: PERSEGUIDOR ESCOLHIDO! ALVO_TEMP_ID=" +
                            target.GetHashCode() + " PERSEGUIDOR_TEMP_ID=" + pursuer.GetHashCode());
                }

                _hauntedUnits = current;
                _reportedScanError = false;
            }
            catch (Exception error)
            {
                if (!_reportedScanError)
                {
                    LogWarning("THE UNRELENTING: ERRO NO SELETOR: " + error);
                    _reportedScanError = true;
                }
            }
        }
    }
}
