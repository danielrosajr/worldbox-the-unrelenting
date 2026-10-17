using System;
using System.Collections.Generic;
using System.Reflection;
using NeoModLoader.api;
using UnityEngine;

namespace Arvak.TheUnrelenting
{
    // v0.10.0 - Origins: geracao rara, mundial e opcional da trait Haunted.
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
        // Probabilidades em porcentagem: lento 50%, normal 35%, rapido 15%.
        // O modo e sorteado novamente ao assumir outro hospedeiro.
        private const int SlowChancePercent = 50;
        private const int NormalChancePercent = 35;
        private enum PursuitSpeed { Slow, Normal, Fast }

        // Arrancada surpresa: cada hospedeiro SLOW ou NORMAL tem uma chance
        // pequena de acelerar por alguns segundos, SEM trocar de corpo.
        // Todos os tempos abaixo sao em segundos REAIS (nao tempo de simulacao).
        private const bool EnableSuddenBurst = true;
        private const int BurstChancePercent = 25; // Por tentativa, nao por segundo.
        private const float FirstBurstCheckMin = 10f;
        private const float FirstBurstCheckMax = 17f;
        private const float NextBurstCheckMin = 12f;
        private const float NextBurstCheckMax = 22f;
        private const float BurstDurationMin = 3f;
        private const float BurstDurationMax = 5.5f;
        private const float MinBurstTargetDistance = 2f;
        private const float MaxBurstTargetDistance = 55f;

        // Apenas as especies comuns predefinidas. IDs podem variar por versao;
        // a lista e verificada contra os assets reais dos atores no mundo.
        private static readonly HashSet<string> AllowedHostAssets = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "human", "elf", "dwarf", "orc", "sheep", "cow", "chicken",
            "rabbit", "cat", "wolf", "fox", "bear", "goat", "alpaca",
            "capybara", "monkey", "rat", "penguin", "raccoon", "armadillo"
        };
        private static readonly string[] CoreTemporaryTraitIds = { "peaceful", "immortal", "super_health" };
        // Nao usar hospedeiros com traits de velocidade preexistentes. Slow e Fast
        // podem se opor: aplicá-las poderia apagar uma trait que nao e do mod.
        private static readonly string[] OppositeRiskTraits = { "slow", "fast", "agile", "plague" };

        // Origins: somente uma nova maldicao automatica por mundo/sessao.
        // Quando desativado, Haunted continua aplicavel pelo jogador normalmente.
        // A leitura dos valores vem do painel nativo do NeoModLoader.
        private const string OriginConfigGroup = "Origins";
        private const string CfgRandomEnabled = "EnableRandomOrigins";
        private const string CfgTestMode = "TestMode";
        private const string CfgChance = "ChancePerAttemptPercent";
        private const string CfgInterval = "AttemptIntervalSeconds";
        private static readonly HashSet<string> EligibleOriginAssets =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "human", "elf", "dwarf", "orc" };

        private struct OriginOptions
        {
            public bool Enabled;
            public bool TestMode;
            public float Chance;
            public float Interval;
        }

        private bool _autoOriginDoneInWorld;
        private bool _originScheduled;
        private bool _lastOriginsEnabled;
        private bool _lastOriginsTestMode;
        private float _nextOriginAttemptAt;
        private bool _warnedOriginSettings;

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
        private PursuitSpeed _currentSpeed = PursuitSpeed.Normal;
        private bool _burstActive;
        private float _burstEndsAt;
        private float _nextBurstCheckAt;
        private bool _burstCleanupWarned;

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
            LogInfo("THE UNRELENTING: V0.10.0 ORIGINS + CADEIA + VELOCIDADE + ARRANCADAS ATIVAS.");
            LogInfo("THE UNRELENTING: ORIGINS: menu do NML > Mods > The Unrelenting > Configuracao.");
            LogInfo("THE UNRELENTING: ORIGINS: aleatoria DESLIGADA por padrao; Haunted manual sempre disponivel.");
            LogInfo("THE UNRELENTING: CHANCES DE VELOCIDADE LENTO=" + SlowChancePercent +
                "% NORMAL=" + NormalChancePercent + "% RAPIDO=" +
                (100 - SlowChancePercent - NormalChancePercent) + "%.");
            LogInfo("THE UNRELENTING: ARRANCADAS CONFIGURADAS! ATIVO=" + EnableSuddenBurst +
                " CHANCE_POR_TENTATIVA=" + BurstChancePercent + "% DURACAO=3-5.5s REAL.");
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

        private OriginOptions ReadOriginOptions()
        {
            OriginOptions options = new OriginOptions
            {
                Enabled = false,
                TestMode = false,
                Chance = 1f,
                Interval = 180f
            };
            try
            {
                // BasicMod.GetConfig(): acesso tipado ao painel nativo do NML.
                // Sao lidas as preferencias gravadas pelo NML a cada scan.
                ModConfig config = GetConfig();
                if (config == null) return options;
                var group = config[OriginConfigGroup];
                options.Enabled = (bool)group[CfgRandomEnabled].GetValue();
                options.TestMode = (bool)group[CfgTestMode].GetValue();
                options.Chance = Convert.ToSingle(group[CfgChance].GetValue());
                options.Interval = Convert.ToSingle(group[CfgInterval].GetValue());
                // Limites extras protegem de configuracoes corrompidas.
                options.Chance = Mathf.Clamp(options.Chance, 0f, 100f);
                options.Interval = Mathf.Clamp(options.Interval, 30f, 1800f);
                _warnedOriginSettings = false;
            }
            catch (Exception e)
            {
                // Falha fechada: nunca gerar maldicao aleatoria sem config valida.
                options.Enabled = false;
                options.TestMode = false;
                if (!_warnedOriginSettings)
                {
                    LogWarning("THE UNRELENTING: ORIGINS CONFIG INDISPONIVEL! ALEATORIO DESATIVADO. " +
                        e.GetType().Name + ": " + e.Message);
                    _warnedOriginSettings = true;
                }
            }
            return options;
        }

        private Actor PickOriginTarget(List<Actor> alive, HashSet<Actor> currentHaunted)
        {
            Actor choice = null;
            int candidates = 0;
            foreach (Actor actor in alive)
            {
                if (!Usable(actor) || currentHaunted.Contains(actor) ||
                    object.ReferenceEquals(actor, _host) || !EligibleOriginAssets.Contains(GetAssetId(actor)))
                    continue;
                candidates++;
                if (_random.Next(candidates) == 0) choice = actor;
            }
            return choice;
        }

        private void UpdateOrigins(List<Actor> alive, HashSet<Actor> currentHaunted)
        {
            OriginOptions options = ReadOriginOptions();
            bool changed = options.Enabled != _lastOriginsEnabled ||
                           options.TestMode != _lastOriginsTestMode;
            if (changed)
            {
                LogInfo("THE UNRELENTING: ORIGINS CONFIG ALTERADA! ALEATORIO=" + options.Enabled +
                    " TESTE_RAPIDO=" + options.TestMode +
                    " CHANCE=" + options.Chance.ToString("F2") +
                    "% INTERVALO_REAL=" + options.Interval.ToString("F0") + "s");
                _originScheduled = false;
                _lastOriginsEnabled = options.Enabled;
                _lastOriginsTestMode = options.TestMode;
            }
            if (!options.Enabled || _autoOriginDoneInWorld) return;

            // Nao iniciar uma segunda origem enquanto qualquer Haunted existir.
            // Inclusive depois da extincao da cadeia, esta versao gera no maximo
            // uma origem automatica por mundo carregado, ate sair/carregar mundo.
            if (currentHaunted.Count > 0 || _targetQueue.Count > 0 || _host != null)
            {
                // Reagendar ao ficar sem Haunted, evitando origem imediata ao final.
                _originScheduled = false;
                return;
            }

            float effectiveInterval = options.TestMode ? 10f : options.Interval;
            float effectiveChance = options.TestMode ? 100f : options.Chance;
            if (!_originScheduled)
            {
                _nextOriginAttemptAt = Time.unscaledTime + effectiveInterval;
                _originScheduled = true;
                LogInfo("THE UNRELENTING: ORIGINS AGENDADA! MODO=" +
                    (options.TestMode ? "TESTE" : "RARO") +
                    " INTERVALO_SEGUNDOS=" + effectiveInterval.ToString("F0") +
                    " CHANCE_POR_TENTATIVA=" + effectiveChance.ToString("F2") + "%");
                return;
            }
            if (Time.unscaledTime < _nextOriginAttemptAt) return;
            _nextOriginAttemptAt = Time.unscaledTime + effectiveInterval;
            float roll = (float)(_random.NextDouble() * 100.0);
            if (roll >= effectiveChance)
            {
                LogInfo("THE UNRELENTING: ORIGINS SORTEIO SEM EVENTO! CHANCE=" +
                    effectiveChance.ToString("F2") + "%");
                return;
            }

            Actor candidate = PickOriginTarget(alive, currentHaunted);
            if (candidate == null)
            {
                LogWarning("THE UNRELENTING: ORIGINS SEM ELEGIVEIS! Necessita humano, elfo, anao ou orc vivo.");
                return;
            }
            int candidateId = candidate.GetHashCode();
            if (MutateTrait(candidate, "addTrait", TraitId) && candidate.hasTrait(TraitId))
            {
                // Garantir que a fila seja atualizada ainda neste mesmo scan.
                currentHaunted.Add(candidate);
                _autoOriginDoneInWorld = true;
                LogInfo("THE UNRELENTING: ORIGINS HAUNTED GERADA! ALVO_TEMP_ID=" +
                    candidateId + " ESPECIE=" + GetAssetId(candidate) +
                    " MODO=" + (options.TestMode ? "TESTE" : "RARO") +
                    " UNICA_NESTA_SESSAO_DE_MUNDO=True");
            }
            else
            {
                LogWarning("THE UNRELENTING: ORIGINS FALHA AO APLICAR HAUNTED! ALVO_TEMP_ID=" + candidateId);
            }
        }

        private PursuitSpeed RollSpeed()
        {
            int roll = _random.Next(100);
            if (roll < SlowChancePercent) return PursuitSpeed.Slow;
            if (roll < SlowChancePercent + NormalChancePercent) return PursuitSpeed.Normal;
            return PursuitSpeed.Fast;
        }

        private void ApplyTemporaryTraits(Actor a, PursuitSpeed speed)
        {
            if (!Usable(a)) return;
            HashSet<string> added = new HashSet<string>();
            List<string> traits = new List<string>(CoreTemporaryTraitIds);
            if (speed == PursuitSpeed.Slow) traits.Insert(0, "slow");
            else if (speed == PursuitSpeed.Fast) traits.Insert(0, "fast");
            // O modo Normal nao aplica trait de movimento; vale a velocidade
            // natural da especie, sem alteracao permanente.
            foreach (string id in traits)
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

        // Gerencia APENAS traits introduzidas por este mod: o hospedeiro
        // nunca perde um Slow/Fast anterior ao periodo de possessao.
        private bool AddOwnedSpeedTrait(Actor a, string id)
        {
            if (!Usable(a)) return false;
            HashSet<string> owned;
            if (!_traitsAddedByMod.TryGetValue(a, out owned))
            {
                owned = new HashSet<string>();
                _traitsAddedByMod[a] = owned;
            }
            if (a.hasTrait(id)) return owned.Contains(id);
            if (!MutateTrait(a, "addTrait", id) || !a.hasTrait(id)) return false;
            owned.Add(id);
            a.setStatsDirty();
            return true;
        }

        private bool RemoveOwnedSpeedTrait(Actor a, string id)
        {
            if (a == null) return false;
            HashSet<string> owned;
            if (!_traitsAddedByMod.TryGetValue(a, out owned) || !owned.Contains(id))
                return false; // Nunca tocar em traits que nao sao nossas.
            if (a.hasTrait(id))
            {
                if (!MutateTrait(a, "removeTrait", id) || a.hasTrait(id)) return false;
            }
            owned.Remove(id);
            a.setStatsDirty();
            return true;
        }

        private float RandomSeconds(float min, float max)
        {
            return min + (float)_random.NextDouble() * (max - min);
        }

        private void StartSuddenBurst()
        {
            if (_burstActive || !Usable(_host) || _currentSpeed == PursuitSpeed.Fast) return;

            bool removedSlow = false;
            if (_currentSpeed == PursuitSpeed.Slow)
            {
                // Para ficar rapido, precisamos liberar o Slow QUE NOS adicionamos.
                if (!RemoveOwnedSpeedTrait(_host, "slow"))
                {
                    LogWarning("THE UNRELENTING: ARRANCADA CANCELADA! Falha ao retirar slow temporario.");
                    return;
                }
                removedSlow = true;
            }

            if (!AddOwnedSpeedTrait(_host, "fast"))
            {
                if (removedSlow && !AddOwnedSpeedTrait(_host, "slow"))
                    LogWarning("THE UNRELENTING: ARRANCADA: falha ao restaurar slow apos erro de fast.");
                LogWarning("THE UNRELENTING: ARRANCADA CANCELADA! Trait fast nao foi aplicada.");
                return;
            }
            _burstActive = true;
            _burstCleanupWarned = false;
            float duration = RandomSeconds(BurstDurationMin, BurstDurationMax);
            _burstEndsAt = Time.unscaledTime + duration;
            LogInfo("THE UNRELENTING: ARRANCADA INICIADA! HOSPEDEIRO_TEMP_ID=" +
                _host.GetHashCode() + " FORMA=" + GetAssetId(_host) +
                " VELOCIDADE_ANTERIOR=" + _currentSpeed.ToString().ToUpperInvariant() +
                " VELOCIDADE_ATUAL=FAST DURACAO_SEGUNDOS=" + duration.ToString("F1"));
        }

        private void EndSuddenBurst()
        {
            if (!_burstActive) return;
            if (!Usable(_host))
            {
                _burstActive = false;
                return;
            }
            // Nao reintroduzimos Slow enquanto nao houver certeza de que Fast saiu.
            if (!RemoveOwnedSpeedTrait(_host, "fast"))
            {
                if (!_burstCleanupWarned)
                {
                    LogWarning("THE UNRELENTING: ARRANCADA: nao foi possivel limpar fast; tentando novamente.");
                    _burstCleanupWarned = true;
                }
                return;
            }
            if (_currentSpeed == PursuitSpeed.Slow && !AddOwnedSpeedTrait(_host, "slow"))
                LogWarning("THE UNRELENTING: ARRANCADA: nao foi possivel restaurar slow neste corpo.");
            LogInfo("THE UNRELENTING: ARRANCADA ENCERRADA! HOSPEDEIRO_TEMP_ID=" +
                _host.GetHashCode() + " VELOCIDADE_RESTAURADA=" +
                _currentSpeed.ToString().ToUpperInvariant());
            _burstActive = false;
            _burstCleanupWarned = false;
            _nextBurstCheckAt = Time.unscaledTime + RandomSeconds(NextBurstCheckMin, NextBurstCheckMax);
        }

        private void UpdateSuddenBurst()
        {
            if (!EnableSuddenBurst || !Usable(_host) || !Usable(_target)) return;
            if (_burstActive)
            {
                if (Time.unscaledTime >= _burstEndsAt) EndSuddenBurst();
                return;
            }
            if (Time.unscaledTime < _nextBurstCheckAt) return;
            _nextBurstCheckAt = Time.unscaledTime + RandomSeconds(NextBurstCheckMin, NextBurstCheckMax);
            if (_currentSpeed == PursuitSpeed.Fast) return;
            float distance = _host.distanceToActorTile(_target);
            if (distance < MinBurstTargetDistance || distance > MaxBurstTargetDistance) return;
            if (_random.Next(100) < BurstChancePercent) StartSuddenBurst();
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
            _currentSpeed = RollSpeed();
            _burstActive = false; // antiga corrida limpa por CleanupTemporaryTraits(old)
            _burstCleanupWarned = false;
            _nextBurstCheckAt = Time.unscaledTime + RandomSeconds(FirstBurstCheckMin, FirstBurstCheckMax);
            ApplyTemporaryTraits(_host, _currentSpeed);
            _nextHostChangeTime = Time.unscaledTime + (isChange ? HostChangeInterval : FirstHostChangeAfter);
            _lastProgressTime = Time.unscaledTime;
            _lastDistance = _target != null ? _host.distanceToActorTile(_target) : 0f;
            LogInfo("THE UNRELENTING: " + (isChange ? "ENTIDADE TROCOU DE HOSPEDEIRO!" : "ENTIDADE DESPERTOU!") +
                " ANTERIOR_TEMP_ID=" + (old == null ? "NENHUM" : old.GetHashCode().ToString()) +
                " NOVO_TEMP_ID=" + next.GetHashCode() +
                " FORMA_ANTERIOR=" + oldSpecies + " NOVA_FORMA=" + GetAssetId(next) +
                " ALVO_TEMP_ID=" + (_target == null ? "NENHUM" : _target.GetHashCode().ToString()) +
                " VELOCIDADE=" + _currentSpeed.ToString().ToUpperInvariant() +
                " TRAIT_VELOCIDADE=" + (_currentSpeed == PursuitSpeed.Slow ? "slow" :
                    (_currentSpeed == PursuitSpeed.Fast ? "fast" : "nenhuma")));
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
            _currentSpeed = PursuitSpeed.Normal;
            _burstActive = false;
            _burstCleanupWarned = false;
            _nextBurstCheckAt = 0f;
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
            _currentSpeed = PursuitSpeed.Normal;
            _burstActive = false;
            _burstCleanupWarned = false;
            _nextBurstCheckAt = 0f;
            _warnedNoHost = false;
            _reportedError = false;
            _autoOriginDoneInWorld = false;
            _originScheduled = false;
            _lastOriginsEnabled = false;
            _lastOriginsTestMode = false;
            _nextOriginAttemptAt = 0f;
            _warnedOriginSettings = false;
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

                // Origins e um sistema separado. Ele aplica Haunted em no maximo
                // UMA unidade elegivel e depois deixa a fila existente funcionar.
                UpdateOrigins(alive, current);

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
                // O mesmo hospedeiro pode acelerar inesperadamente.
                UpdateSuddenBurst();
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
