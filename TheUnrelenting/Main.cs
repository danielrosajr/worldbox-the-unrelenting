using System;
using System.Collections.Generic;
using System.Reflection;
using NeoModLoader.api;
using UnityEngine;

namespace Arvak.TheUnrelenting
{
    // v0.7.0 - UMA entidade, multiplas vitimas em fila, corpo hospedeiro trocavel.
    // "Troca de corpo" NAO modifica a especie ou o sprite de um unico Actor:
    // a entidade assume temporariamente OUTRA criatura existente. O antigo corpo
    // recupera as traits que o mod adicionou e volta a ser uma unidade comum.
    public class Main : BasicMod<Main>
    {
        public const string TraitId = "ArvakTheUnrelenting_Haunted";
        public const string GroupId = "ArvakTheUnrelenting_Group";

        private const float ScanInterval = 2f;
        private const float ContactDistance = 1.5f;
        private const float MinStartDistance = 4f;
        private const float PreferMaxDistance = 50f;
        private const float ProgressLogInterval = 15f;
        private const float FirstHostChangeAfter = 22f;
        private const float HostChangeInterval = 32f;
        private const float MinHostHopDistance = 3f;
        private const float MaxHostHopDistance = 24f;
        private const bool EnableHostHopping = true;

        // Apenas as especies comuns predefinidas. IDs podem variar por versao;
        // a lista e verificada contra os assets reais dos atores no mundo.
        private static readonly HashSet<string> AllowedHostAssets = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "human", "elf", "dwarf", "orc", "sheep", "cow", "chicken",
            "rabbit", "cat", "wolf", "fox", "bear", "goat", "alpaca",
            "capybara", "monkey", "rat", "penguin", "raccoon", "armadillo"
        };
        private static readonly string[] TemporaryTraitIds = { "slow", "peaceful", "immortal", "super_health" };
        // Prevenir a perda involuntaria de traits com possiveis opostos.
        private static readonly string[] OppositeRiskTraits = { "fast", "agile", "plague" };

        private readonly System.Random _random = new System.Random();
        private readonly List<Actor> _targetQueue = new List<Actor>();
        private readonly HashSet<Actor> _knownHaunted = new HashSet<Actor>();
        private readonly Dictionary<Actor, HashSet<string>> _traitsAddedByMod =
            new Dictionary<Actor, HashSet<string>>();

        private Actor _host;
        private Actor _target;
        private object _activeWorldReference;
        private float _scanClock;
        private float _lastProgressTime;
        private float _lastDistance;
        private float _nextHostChangeTime;
        private bool _reportedError;
        private bool _warnedNoHost;
        private int _killsThisChain;

        protected override void OnModLoad()
        {
            LogInfo("THE UNRELENTING: MOD CARREGADO COM SUCESSO!");
            AssetManager.trait_groups.add(new ActorTraitGroupAsset
            {
                id = GroupId,
                name = "trait_group_ArvakTheUnrelenting",
                color = "#9755C7"
            });
            AssetManager.traits.add(new ActorTrait
            {
                id = TraitId,
                group_id = GroupId,
                path_icon = "ui/icons/neomodloader",
                can_be_given = true,
                can_be_removed = true,
                can_be_cured = false,
                needs_to_be_explored = false
            });
            LogInfo("THE UNRELENTING: TRAIT HAUNTED REGISTRADA COM SUCESSO! ID=" + TraitId);
            LogInfo("THE UNRELENTING: V0.7.0 CADEIA DE VITIMAS + TROCA EXPERIMENTAL DE HOSPEDEIRO ATIVA.");
        }

        private static bool Usable(Actor a)
        {
            return a != null && a.isAlive() && a.current_tile != null;
        }

        // Reflection apenas para mutacoes de traits, pois os overloads mudaram
        // entre versoes. Ao registrar/limpar, checamos o resultado de hasTrait.
        private static bool MutateTrait(Actor a, string methodName, string traitId)
        {
            if (a == null) return false;
            foreach (MethodInfo method in a.GetType().GetMethods(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (method.Name != methodName) continue;
                ParameterInfo[] ps = method.GetParameters();
                if (ps.Length == 0 || ps[0].ParameterType != typeof(string)) continue;
                object[] args = new object[ps.Length];
                args[0] = traitId;
                bool supported = true;
                for (int i = 1; i < ps.Length; i++)
                {
                    if (ps[i].HasDefaultValue) args[i] = ps[i].DefaultValue;
                    else if (ps[i].ParameterType == typeof(bool)) args[i] = false;
                    else if (ps[i].ParameterType.IsValueType)
                        args[i] = Activator.CreateInstance(ps[i].ParameterType);
                    else supported = false;
                }
                if (!supported) continue;
                try { method.Invoke(a, args); return true; }
                catch { /* proximo overload */ }
            }
            return false;
        }

        private void ApplyTemporaryTraits(Actor a)
        {
            if (!Usable(a)) return;
            HashSet<string> added = new HashSet<string>();
            foreach (string id in TemporaryTraitIds)
            {
                if (a.hasTrait(id)) continue;
                bool invoked = MutateTrait(a, "addTrait", id);
                if (invoked && a.hasTrait(id))
                {
                    added.Add(id);
                    LogInfo("THE UNRELENTING: TRAIT TEMPORARIA APLICADA HOSPEDEIRO_TEMP_ID=" +
                        a.GetHashCode() + " TRAIT=" + id);
                }
                else
                {
                    LogWarning("THE UNRELENTING: NAO FOI POSSIVEL APLICAR TRAIT=" + id +
                        " HOSPEDEIRO_TEMP_ID=" + a.GetHashCode());
                }
            }
            if (added.Count != 0) _traitsAddedByMod[a] = added;
            a.setStatsDirty();
        }

        private void CleanupTemporaryTraits(Actor a)
        {
            if (a == null) return;
            HashSet<string> ids;
            if (!_traitsAddedByMod.TryGetValue(a, out ids)) return;
            if (a.isAlive())
            {
                foreach (string id in ids)
                {
                    if (!a.hasTrait(id)) continue;
                    bool ok = MutateTrait(a, "removeTrait", id) && !a.hasTrait(id);
                    LogInfo("THE UNRELENTING: LIMPEZA TRAIT=" + id +
                        " HOSPEDEIRO_TEMP_ID=" + a.GetHashCode() +
                        " RESULTADO=" + (ok ? "OK" : "VERIFICAR"));
                }
                a.setStatsDirty();
            }
            _traitsAddedByMod.Remove(a);
        }

        // Leitura defensiva do tipo real do Actor: nenhuma mudanca no asset.
        private static string GetAssetId(Actor actor)
        {
            if (actor == null) return "";
            try
            {
                object asset = actor.getActorAsset();
                if (asset == null) return "";
                Type t = asset.GetType();
                FieldInfo field = t.GetField("id", BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
                if (field != null) return Convert.ToString(field.GetValue(asset)) ?? "";
                PropertyInfo property = t.GetProperty("id", BindingFlags.Public | BindingFlags.Instance);
                if (property != null) return Convert.ToString(property.GetValue(asset, null)) ?? "";
            }
            catch { /* desconhecido -> nao seleciona */ }
            return "";
        }

        private static bool IsAllowedHost(Actor a, HashSet<Actor> haunted, Actor except)
        {
            if (!Usable(a) || object.ReferenceEquals(a, except) || haunted.Contains(a))
                return false;
            foreach (string t in OppositeRiskTraits)
                if (a.hasTrait(t)) return false;
            return AllowedHostAssets.Contains(GetAssetId(a));
        }

        // Reservoir sampling sem alocar todas as criaturas candidatas.
        private Actor PickHost(List<Actor> alive, HashSet<Actor> haunted, Actor target, Actor around)
        {
            Actor near = null;
            Actor fallback = null;
            int nearCount = 0;
            int fallbackCount = 0;
            foreach (Actor a in alive)
            {
                if (!IsAllowedHost(a, haunted, around)) continue;
                float d = a.distanceToActorTile(around ?? target);
                if (around != null)
                {
                    // Para mudanca de hospedeiro, escolher proximo ao antigo.
                    if (d < MinHostHopDistance || d > MaxHostHopDistance) continue;
                    nearCount++;
                    if (_random.Next(nearCount) == 0) near = a;
                }
                else
                {
                    if (d < MinStartDistance) continue;
                    fallbackCount++;
                    if (_random.Next(fallbackCount) == 0) fallback = a;
                    if (d <= PreferMaxDistance)
                    {
                        nearCount++;
                        if (_random.Next(nearCount) == 0) near = a;
                    }
                }
            }
            return near ?? fallback;
        }

        private void SetHost(Actor next, bool isChange)
        {
            if (!Usable(next)) return;
            Actor old = _host;
            string oldSpecies = GetAssetId(old);
            if (old != null && !object.ReferenceEquals(old, next))
            {
                CleanupTemporaryTraits(old);
                // Nao matamos o antigo hospedeiro. Ele apenas deixa de ser a entidade.
                if (Usable(old)) old.cancelAllBeh();
            }
            _host = next;
            _host.cancelAllBeh();
            ApplyTemporaryTraits(_host);
            _nextHostChangeTime = Time.unscaledTime + (isChange ? HostChangeInterval : FirstHostChangeAfter);
            _lastProgressTime = Time.unscaledTime;
            _lastDistance = _target != null ? _host.distanceToActorTile(_target) : 0f;
            LogInfo("THE UNRELENTING: " + (isChange ? "ENTIDADE TROCOU DE HOSPEDEIRO!" : "ENTIDADE DESPERTOU!") +
                " ANTERIOR_TEMP_ID=" + (old == null ? "NENHUM" : old.GetHashCode().ToString()) +
                " NOVO_TEMP_ID=" + next.GetHashCode() +
                " FORMA_ANTERIOR=" + oldSpecies + " NOVA_FORMA=" + GetAssetId(next) +
                " ALVO_TEMP_ID=" + (_target == null ? "NENHUM" : _target.GetHashCode().ToString()));
        }

        private void EndEntity(string reason, bool vanish)
        {
            if (_host != null)
            {
                Actor old = _host;
                CleanupTemporaryTraits(old);
                if (vanish && Usable(old))
                {
                    // Como o host pertence a entidade neste momento, ele desaparece.
                    old.dieSimpleNone();
                }
                LogInfo("THE UNRELENTING: ENTIDADE ENCERRADA! MOTIVO=" + reason +
                    " ULTIMO_HOSPEDEIRO_TEMP_ID=" + old.GetHashCode() +
                    " DESAPARECEU=" + (vanish && !old.isAlive()) +
                    " EXECUCOES=" + _killsThisChain);
            }
            _host = null;
            _target = null;
            _killsThisChain = 0;
            _warnedNoHost = false;
        }

        private void ResetForWorld()
        {
            // Se outro mundo foi carregado, nao chamar die() em objetos antigos.
            foreach (Actor actor in new List<Actor>(_traitsAddedByMod.Keys))
                CleanupTemporaryTraits(actor);
            _traitsAddedByMod.Clear();
            _knownHaunted.Clear();
            _targetQueue.Clear();
            _host = null;
            _target = null;
            _killsThisChain = 0;
            _warnedNoHost = false;
            _reportedError = false;
        }

        private void Update()
        {
            _scanClock += Time.unscaledDeltaTime;
            if (_scanClock < ScanInterval) return;
            _scanClock = 0f;
            if (World.world == null)
            {
                ResetForWorld();
                _activeWorldReference = null;
                return;
            }
            if (!object.ReferenceEquals(_activeWorldReference, World.world))
            {
                ResetForWorld();
                _activeWorldReference = World.world;
            }

            try
            {
                List<Actor> alive = new List<Actor>();
                HashSet<Actor> current = new HashSet<Actor>();
                foreach (Actor actor in World.world.units)
                {
                    if (!Usable(actor)) continue;
                    alive.Add(actor);
                    if (actor.hasTrait(TraitId)) current.Add(actor);
                }

                // A fila respeita a ordem de descoberta; nao abre novos perseguidores.
                foreach (Actor actor in alive)
                {
                    if (!current.Contains(actor) || _knownHaunted.Contains(actor)) continue;
                    _targetQueue.Add(actor);
                    LogInfo("THE UNRELENTING: HAUNTED ENTROU NA FILA! ALVO_TEMP_ID=" +
                        actor.GetHashCode() + " FILA=" + _targetQueue.Count);
                }
                foreach (Actor old in _knownHaunted)
                {
                    if (current.Contains(old)) continue;
                    _targetQueue.Remove(old);
                    string reason = !old.isAlive() ? "MORTE_OU_DESAPARECIMENTO" :
                        (old.current_tile == null ? "SEM_TILE" : "TRAIT_REMOVIDA");
                    LogInfo("THE UNRELENTING: HAUNTED SAIU DA FILA! ALVO_TEMP_ID=" +
                        old.GetHashCode() + " MOTIVO=" + reason);
                }
                _knownHaunted.Clear();
                foreach (Actor a in current) _knownHaunted.Add(a);

                // Alvo atual sempre e a primeira vitima ainda ativa na fila.
                Actor nextTarget = _targetQueue.Count == 0 ? null : _targetQueue[0];
                if (!object.ReferenceEquals(nextTarget, _target))
                {
                    Actor prev = _target;
                    _target = nextTarget;
                    if (_target != null)
                    {
                        LogInfo("THE UNRELENTING: ALVO DEFINIDO! ANTERIOR_TEMP_ID=" +
                            (prev == null ? "NENHUM" : prev.GetHashCode().ToString()) +
                            " NOVO_TEMP_ID=" + _target.GetHashCode() +
                            " HAUNTED_RESTANTES=" + _targetQueue.Count);
                    }
                }

                if (_target == null)
                {
                    if (_host != null) EndEntity("SEM_VITIMAS", true);
                    return;
                }

                // Evitar usar a propria vitima como corpo da entidade.
                if (_host != null && current.Contains(_host))
                {
                    LogWarning("THE UNRELENTING: HOSPEDEIRO RECEBEU HAUNTED; SOLTANDO CORPO.");
                    EndEntity("HOSPEDEIRO_MARCADO", false);
                    _target = _targetQueue[0];
                }
                if (!Usable(_host))
                {
                    if (_host != null)
                        LogWarning("THE UNRELENTING: HOSPEDEIRO MORREU; ENTIDADE PROCURA OUTRO CORPO.");
                    _host = null;
                    Actor chosen = PickHost(alive, current, _target, null);
                    if (chosen == null)
                    {
                        if (!_warnedNoHost)
                            LogWarning("THE UNRELENTING: SEM HOSPEDEIRO COMPATIVEL. Crie humanos, ovelhas ou vacas.");
                        _warnedNoHost = true;
                        return;
                    }
                    _warnedNoHost = false;
                    SetHost(chosen, false);
                }

                if (EnableHostHopping && Time.unscaledTime >= _nextHostChangeTime)
                {
                    Actor newHost = PickHost(alive, current, _target, _host);
                    if (newHost != null && !object.ReferenceEquals(newHost, _host))
                        SetHost(newHost, true);
                    else
                        _nextHostChangeTime = Time.unscaledTime + 10f;
                }

                if (!Usable(_target) || !Usable(_host)) return;
                float distance = _host.distanceToActorTile(_target);
                if (distance <= ContactDistance)
                {
                    int victimId = _target.GetHashCode();
                    int hostId = _host.GetHashCode();
                    LogInfo("THE UNRELENTING: CONTATO COM A VITIMA! ALVO_TEMP_ID=" + victimId +
                        " HOSPEDEIRO_TEMP_ID=" + hostId);
                    _target.dieSimpleNone();
                    if (!_target.isAlive())
                    {
                        _killsThisChain++;
                        LogInfo("THE UNRELENTING: EXECUCAO CONFIRMADA! ALVO_TEMP_ID=" + victimId +
                            " HOSPEDEIRO_TEMP_ID=" + hostId + " TOTAL=" + _killsThisChain);
                        _targetQueue.Remove(_target);
                        _knownHaunted.Remove(_target);
                        _target = _targetQueue.Count > 0 ? _targetQueue[0] : null;
                        if (_target != null)
                        {
                            LogInfo("THE UNRELENTING: PROXIMA VITIMA! NOVO_ALVO_TEMP_ID=" +
                                _target.GetHashCode() + " HOSPEDEIRO_TEMP_ID=" + hostId +
                                " RESTANTES=" + _targetQueue.Count);
                        }
                        else
                        {
                            EndEntity("CADEIA_CONCLUIDA", true);
                        }
                        return;
                    }
                    LogWarning("THE UNRELENTING: EXECUCAO NAO CONFIRMADA! ALVO_TEMP_ID=" + victimId);
                }
                else
                {
                    _host.goTo(_target.current_tile, false);
                }

                if (Time.unscaledTime - _lastProgressTime >= ProgressLogInterval)
                {
                    LogInfo("THE UNRELENTING: PROGRESSO CADEIA ALVO_TEMP_ID=" +
                        _target.GetHashCode() + " HOSPEDEIRO_TEMP_ID=" + _host.GetHashCode() +
                        " DISTANCIA=" + distance.ToString("F1") +
                        " DISTANCIA_ANTERIOR=" + _lastDistance.ToString("F1") +
                        " RESTANTES=" + _targetQueue.Count);
                    _lastDistance = distance;
                    _lastProgressTime = Time.unscaledTime;
                }
                _reportedError = false;
            }
            catch (Exception e)
            {
                if (!_reportedError)
                {
                    LogWarning("THE UNRELENTING: ERRO NA CADEIA DE PERSEGUICAO: " + e);
                    _reportedError = true;
                }
            }
        }
    }
}
