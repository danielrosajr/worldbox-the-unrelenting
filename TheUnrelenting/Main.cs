using System;
using System.Collections.Generic;
using NeoModLoader.api;
using UnityEngine;

namespace Arvak.TheUnrelenting
{
    public class Main : BasicMod<Main>
    {
        // IDs persistentes: NAO alterar entre versoes, para preservar a trait nos saves.
        public const string TraitId = "ArvakTheUnrelenting_Haunted";
        public const string GroupId = "ArvakTheUnrelenting_Group";

        // A v0.4.0 exercita APENAS a navegacao: sem danos, invulnerabilidade ou buffs.
        // Preferimos candidatos proximos para facilitar o teste. Nao ha ainda
        // verificacao de ilha, entao mares, paredes ou montanhas podem bloquear.
        private const float MinStartDistance = 4f;
        private const float PreferredMaxDistance = 55f;
        private const float ScanInterval = 2f;
        private const float ProgressLogInterval = 15f;

        private HashSet<Actor> _hauntedUnits = new HashSet<Actor>();
        private readonly Dictionary<Actor, Actor> _pursuerByTarget = new Dictionary<Actor, Actor>();
        private readonly HashSet<Actor> _noCandidateNotice = new HashSet<Actor>();
        private readonly Dictionary<Actor, float> _lastLoggedDistance = new Dictionary<Actor, float>();
        private readonly Dictionary<Actor, float> _lastProgressTime = new Dictionary<Actor, float>();
        private readonly HashSet<Actor> _reachedTargets = new HashSet<Actor>();
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
            LogInfo("THE UNRELENTING: PERSEGUICAO EXPERIMENTAL ATIVA (atualizacao a cada 2 segundos; SEM MORTE). ");
        }

        private void ResetTracking()
        {
            _hauntedUnits.Clear();
            _pursuerByTarget.Clear();
            _noCandidateNotice.Clear();
            _lastLoggedDistance.Clear();
            _lastProgressTime.Clear();
            _reachedTargets.Clear();
        }

        private void ForgetTarget(Actor target)
        {
            _pursuerByTarget.Remove(target);
            _noCandidateNotice.Remove(target);
            _lastLoggedDistance.Remove(target);
            _lastProgressTime.Remove(target);
            _reachedTargets.Remove(target);
        }

        private static bool HasUsableTile(Actor actor)
        {
            return actor != null && actor.isAlive() && actor.current_tile != null;
        }

        // Reservoir sampling entre candidatos proximos e nao reservados.
        // Se nao houver criatura proxima, usa uma distante como fallback para nao
        // quebrar o fluxo da versao 0.3.0; nesse caso a viagem pode falhar.
        private Actor PickPursuer(List<Actor> alive, HashSet<Actor> haunted,
                                  HashSet<Actor> reserved, Actor target)
        {
            Actor nearby = null;
            Actor fallback = null;
            int nearbyCount = 0;
            int fallbackCount = 0;

            foreach (Actor candidate in alive)
            {
                if (object.ReferenceEquals(candidate, target) ||
                    haunted.Contains(candidate) || reserved.Contains(candidate) ||
                    !HasUsableTile(candidate))
                    continue;

                float distance = candidate.distanceToActorTile(target);
                if (distance < MinStartDistance)
                    continue;

                fallbackCount++;
                if (_random.Next(fallbackCount) == 0)
                    fallback = candidate;

                if (distance <= PreferredMaxDistance)
                {
                    nearbyCount++;
                    if (_random.Next(nearbyCount) == 0)
                        nearby = candidate;
                }
            }

            return nearby ?? fallback;
        }

        private void Update()
        {
            _scanClock += Time.unscaledDeltaTime;
            if (_scanClock < ScanInterval)
                return;
            _scanClock = 0f;

            if (World.world == null)
            {
                ResetTracking();
                _activeWorldReference = null;
                return;
            }

            // Mesma protecao usada na v0.3.0 contra referencias de outro mundo.
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
                    if (!HasUsableTile(actor))
                        continue;

                    alive.Add(actor);
                    if (actor.hasTrait(TraitId))
                    {
                        current.Add(actor);
                        if (!_hauntedUnits.Contains(actor))
                            LogInfo("THE UNRELENTING: HAUNTED DETECTADO! TEMP_ID=" + actor.GetHashCode());
                    }
                }

                foreach (Actor previous in _hauntedUnits)
                {
                    if (!current.Contains(previous))
                    {
                        LogInfo("THE UNRELENTING: HAUNTED DEIXOU DE ESTAR ATIVO! TEMP_ID=" + previous.GetHashCode());
                        ForgetTarget(previous);
                    }
                }

                // Desativa vinculos invalidos, sem tocar em unidades mortas.
                List<Actor> invalid = new List<Actor>();
                foreach (KeyValuePair<Actor, Actor> pair in _pursuerByTarget)
                {
                    if (!current.Contains(pair.Key) || !HasUsableTile(pair.Value) ||
                        current.Contains(pair.Value))
                        invalid.Add(pair.Key);
                }
                foreach (Actor target in invalid)
                {
                    if (current.Contains(target))
                        LogInfo("THE UNRELENTING: PERSEGUIDOR INDISPONIVEL; NOVA ESCOLHA NECESSARIA. ALVO_TEMP_ID=" + target.GetHashCode());
                    ForgetTarget(target);
                }

                HashSet<Actor> reserved = new HashSet<Actor>(_pursuerByTarget.Values);
                foreach (Actor target in current)
                {
                    if (_pursuerByTarget.ContainsKey(target))
                        continue;

                    Actor pursuer = PickPursuer(alive, current, reserved, target);
                    if (pursuer == null)
                    {
                        if (_noCandidateNotice.Add(target))
                            LogWarning("THE UNRELENTING: NENHUM PERSEGUIDOR DISPONIVEL PARA ALVO_TEMP_ID=" + target.GetHashCode());
                        continue;
                    }

                    _noCandidateNotice.Remove(target);
                    _pursuerByTarget[target] = pursuer;
                    reserved.Add(pursuer);
                    float distance = pursuer.distanceToActorTile(target);
                    _lastLoggedDistance[target] = distance;
                    _lastProgressTime[target] = Time.unscaledTime;

                    LogInfo("THE UNRELENTING: PERSEGUIDOR ESCOLHIDO! ALVO_TEMP_ID=" +
                            target.GetHashCode() + " PERSEGUIDOR_TEMP_ID=" + pursuer.GetHashCode() +
                            " DISTANCIA_INICIAL=" + distance.ToString("F1"));

                    // Uma unica vez ao escolher: a rotina original pode disputar
                    // o movimento; o goTo sera renovado nas varreduras seguintes.
                    pursuer.cancelAllBeh();
                }

                // Reenvia destino movel sem teleporte nem dano. Em alguns casos a
                // IA nativa pode disputar o controle; isso sera refinado depois.
                foreach (KeyValuePair<Actor, Actor> pair in _pursuerByTarget)
                {
                    Actor target = pair.Key;
                    Actor pursuer = pair.Value;
                    if (!current.Contains(target) || !HasUsableTile(pursuer))
                        continue;

                    float distance = pursuer.distanceToActorTile(target);
                    if (distance <= 1.5f)
                    {
                        if (_reachedTargets.Add(target))
                            LogInfo("THE UNRELENTING: PERSEGUIDOR ALCANCOU O ALVO (SEM MATAR)! ALVO_TEMP_ID=" +
                                    target.GetHashCode() + " PERSEGUIDOR_TEMP_ID=" + pursuer.GetHashCode());
                    }
                    else
                    {
                        _reachedTargets.Remove(target);
                        pursuer.goTo(target.current_tile, false);
                    }

                    float lastTime;
                    _lastProgressTime.TryGetValue(target, out lastTime);
                    if (Time.unscaledTime - lastTime >= ProgressLogInterval)
                    {
                        float previousDistance;
                        if (!_lastLoggedDistance.TryGetValue(target, out previousDistance))
                            previousDistance = distance;
                        LogInfo("THE UNRELENTING: PROGRESSO PERSEGUICAO ALVO_TEMP_ID=" + target.GetHashCode() +
                                " PERSEGUIDOR_TEMP_ID=" + pursuer.GetHashCode() +
                                " DISTANCIA=" + distance.ToString("F1") +
                                " DISTANCIA_ANTERIOR=" + previousDistance.ToString("F1"));
                        _lastLoggedDistance[target] = distance;
                        _lastProgressTime[target] = Time.unscaledTime;
                    }
                }

                _hauntedUnits = current;
                _reportedScanError = false;
            }
            catch (Exception error)
            {
                if (!_reportedScanError)
                {
                    LogWarning("THE UNRELENTING: ERRO NA PERSEGUICAO: " + error);
                    _reportedScanError = true;
                }
            }
        }
    }
}
