using System;
using System.Collections.Generic;
using System.Reflection;
using NeoModLoader.api;
using UnityEngine;

namespace Arvak.TheUnrelenting
{
    public class Main : BasicMod<Main>
    {
        // IDs persistentes: NAO alterar entre versoes, para preservar a trait nos saves.
        public const string TraitId = "ArvakTheUnrelenting_Haunted";
        public const string GroupId = "ArvakTheUnrelenting_Group";

        // v0.6.0: execucao TESTAVEL ao contato. Traits nativas sao provisórias;
        // elas NAO garantem invulnerabilidade nem neutralidade absoluta.
        // Nao use em saves importantes: o jogo pode salvar modificadores temporarios.
        private static readonly string[] TestTraits = {
            "slow", "peaceful", "immortal", "super_health"
        };
        // Nao escolha unidades que possam perder Fast/Agile/Plague por opostos.
        private static readonly string[] OppositeRiskTraits = {
            "fast", "agile", "plague"
        };
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
        // Guarda SOMENTE traits que nosso mod adicionou. Nao remover traits preexistentes.
        private readonly Dictionary<Actor, HashSet<string>> _temporaryTraits =
            new Dictionary<Actor, HashSet<string>>();
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
            LogInfo("THE UNRELENTING: V0.6.0 CONTATO LETAL ATIVO (TESTE EM MUNDO DESCARTAVEL).");
        }

        private void ResetTracking()
        {
            CleanupAllTemporaryTraits();
            _hauntedUnits.Clear();
            _pursuerByTarget.Clear();
            _noCandidateNotice.Clear();
            _lastLoggedDistance.Clear();
            _lastProgressTime.Clear();
            _reachedTargets.Clear();
            _temporaryTraits.Clear();
        }

        private void ForgetTarget(Actor target)
        {
            Actor pursuer;
            if (_pursuerByTarget.TryGetValue(target, out pursuer))
                CleanupTemporaryTraits(pursuer);
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

        // Reflective method dispatch: avoids compile errors when a WorldBox build
        // changes the signature of addTrait/removeTrait. Failures stay in the log.
        private static bool InvokeTraitMutation(Actor actor, string methodName, string traitId)
        {
            if (actor == null) return false;
            MethodInfo[] methods = actor.GetType().GetMethods(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            foreach (MethodInfo method in methods)
            {
                if (method.Name != methodName) continue;
                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length < 1 || parameters[0].ParameterType != typeof(string))
                    continue;

                object[] args = new object[parameters.Length];
                args[0] = traitId;
                bool supported = true;
                for (int i = 1; i < parameters.Length; i++)
                {
                    if (parameters[i].HasDefaultValue)
                        args[i] = parameters[i].DefaultValue;
                    else if (parameters[i].ParameterType == typeof(bool))
                        args[i] = false;
                    else if (parameters[i].ParameterType.IsValueType)
                        args[i] = Activator.CreateInstance(parameters[i].ParameterType);
                    else
                        supported = false;
                }
                if (!supported) continue;
                try
                {
                    method.Invoke(actor, args);
                    return true;
                }
                catch
                {
                    // Different overload; test the next one, if any.
                }
            }
            return false;
        }

        private void ApplyTemporaryTraits(Actor pursuer)
        {
            if (!HasUsableTile(pursuer)) return;
            HashSet<string> added = new HashSet<string>();
            foreach (string traitId in TestTraits)
            {
                if (pursuer.hasTrait(traitId)) continue;
                bool called = InvokeTraitMutation(pursuer, "addTrait", traitId);
                if (called && pursuer.hasTrait(traitId))
                {
                    added.Add(traitId);
                    LogInfo("THE UNRELENTING: TRAIT TEMPORARIA APLICADA PERSEGUIDOR_TEMP_ID=" +
                            pursuer.GetHashCode() + " TRAIT=" + traitId);
                }
                else
                {
                    LogWarning("THE UNRELENTING: NAO FOI POSSIVEL APLICAR TRAIT=" + traitId +
                               " PERSEGUIDOR_TEMP_ID=" + pursuer.GetHashCode() +
                               " (verificar ID/API no log)");
                }
            }
            if (added.Count > 0) _temporaryTraits[pursuer] = added;
            pursuer.setStatsDirty();
        }

        private void CleanupTemporaryTraits(Actor pursuer)
        {
            if (pursuer == null) return;
            HashSet<string> added;
            if (!_temporaryTraits.TryGetValue(pursuer, out added)) return;
            if (pursuer.isAlive())
            {
                foreach (string id in added)
                {
                    if (!pursuer.hasTrait(id)) continue;
                    bool called = InvokeTraitMutation(pursuer, "removeTrait", id);
                    LogInfo("THE UNRELENTING: LIMPEZA TRAIT=" + id +
                            " PERSEGUIDOR_TEMP_ID=" + pursuer.GetHashCode() +
                            " RESULTADO=" + (called && !pursuer.hasTrait(id) ? "OK" : "VERIFICAR"));
                }
                pursuer.setStatsDirty();
            }
            _temporaryTraits.Remove(pursuer);
        }

        private void CleanupAllTemporaryTraits()
        {
            var units = new List<Actor>(_temporaryTraits.Keys);
            foreach (Actor actor in units)
                CleanupTemporaryTraits(actor);
        }

        private static bool HasOppositeRisk(Actor actor)
        {
            foreach (string traitId in OppositeRiskTraits)
                if (actor.hasTrait(traitId)) return true;
            return false;
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
                    !HasUsableTile(candidate) || HasOppositeRisk(candidate))
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
                        string reason = !previous.isAlive() ? "MORTE_OU_DESAPARECIMENTO" :
                            (previous.current_tile == null ? "SEM_TILE" :
                             (!previous.hasTrait(TraitId) ? "TRAIT_REMOVIDA" : "FORA_DA_LISTA"));
                        LogInfo("THE UNRELENTING: HAUNTED DEIXOU DE ESTAR ATIVO! TEMP_ID=" +
                                previous.GetHashCode() + " MOTIVO=" + reason);
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
                    ApplyTemporaryTraits(pursuer);
                }

                // Reenvia destino movel. Ao chegar a 1.5 tiles, elimina a vitima
                // pelo ciclo de vida nativo (nao usa dano enorme, que poderia
                // acertar terceiros). Depois remove o proprio perseguidor.
                // ATENCAO: metodo destrutivo; usar somente em mundo descartavel.
                List<Actor> completedTargets = new List<Actor>();
                foreach (KeyValuePair<Actor, Actor> pair in _pursuerByTarget)
                {
                    Actor target = pair.Key;
                    Actor pursuer = pair.Value;
                    if (!current.Contains(target) || !HasUsableTile(target) ||
                        !HasUsableTile(pursuer))
                        continue;

                    float distance = pursuer.distanceToActorTile(target);
                    if (distance <= 1.5f)
                    {
                        // Guard contra chamadas repetidas enquanto o mesmo par
                        // permanece ao alcance, inclusive no ciclo seguinte.
                        if (_reachedTargets.Add(target))
                        {
                            int targetId = target.GetHashCode();
                            int pursuerId = pursuer.GetHashCode();
                            LogInfo("THE UNRELENTING: CONTATO COM A VITIMA! ALVO_TEMP_ID=" +
                                    targetId + " PERSEGUIDOR_TEMP_ID=" + pursuerId);

                            try
                            {
                                // Verificado para WorldBox 0.51.2 (build 719).
                                target.dieSimpleNone();
                                if (!target.isAlive())
                                {
                                    LogInfo("THE UNRELENTING: EXECUCAO CONFIRMADA! ALVO_TEMP_ID=" +
                                            targetId + " PERSEGUIDOR_TEMP_ID=" + pursuerId);

                                    // Sempre desfazer primeiro os modificadores
                                    // adicionados pelo mod. A criatura de teste
                                    // pode ser um habitante normal preexistente.
                                    CleanupTemporaryTraits(pursuer);
                                    try
                                    {
                                        if (pursuer.isAlive())
                                            pursuer.dieSimpleNone();
                                        LogInfo("THE UNRELENTING: PERSEGUIDOR ENCERRADO! PERSEGUIDOR_TEMP_ID=" +
                                                pursuerId + " MORTO=" + (!pursuer.isAlive()));
                                    }
                                    catch (Exception pursuerError)
                                    {
                                        LogWarning("THE UNRELENTING: FALHA AO ENCERRAR PERSEGUIDOR_TEMP_ID=" +
                                                   pursuerId + " ERRO=" + pursuerError);
                                    }

                                    completedTargets.Add(target);
                                    continue; // Nao mandar novo goTo nem logar distancia de morto.
                                }
                                LogWarning("THE UNRELENTING: EXECUCAO SEM CONFIRMACAO! ALVO_TEMP_ID=" + targetId);
                            }
                            catch (Exception executionError)
                            {
                                LogWarning("THE UNRELENTING: FALHA NA EXECUCAO ALVO_TEMP_ID=" +
                                           targetId + " ERRO=" + executionError);
                            }
                            // Permite novo teste em uma verificacao futura caso
                            // a API tenha falhado, mantendo o mundo vivo.
                            _reachedTargets.Remove(target);
                        }
                    }
                    else
                    {
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

                // Adiar a remocao ate depois do foreach do Dictionary.
                foreach (Actor completed in completedTargets)
                {
                    ForgetTarget(completed);
                    current.Remove(completed);
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
