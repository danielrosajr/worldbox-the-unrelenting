using System;
using System.Collections.Generic;
using System.Reflection;
using NeoModLoader.api;
using UnityEngine;

namespace Arvak.TheUnrelenting
{
    // v0.11.4 - Haunted: Trauma: marca visual propria para maldicoes de origem traumatica. Perseguicao compartilhada com Haunted.
    // "Troca de corpo" NAO modifica a especie ou o sprite de um unico Actor:
    // a entidade assume temporariamente OUTRA criatura existente. O antigo corpo
    // recupera as traits que o mod adicionou e volta a ser uma unidade comum.
    public class Main : BasicMod<Main>
    {
        // Mantenha o ID original inalterado: saves antigos continuam legiveis.
        public const string TraitId = "ArvakTheUnrelenting_Haunted";
        // Novo ID permanente. Qualquer unidade com UMA destas duas traits
        // entra na mesma fila e e perseguida pela mesma entidade.
        public const string TraumaTraitId = "ArvakTheUnrelenting_HauntedTrauma";
        public const string GroupId = "ArvakTheUnrelenting_Group";
        private const string AntiKiteBoostTraitId = "ArvakTheUnrelenting_PunishingRush";
        // Trait temporaria e nao selecionavel. Registrada apos AssetManager.traits.add().
        // Atributo fixo proposital para nao mutar o mesmo asset global em runtime.
        private const float PunishingRushSpeedBonus = 75f;

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

        // Origins: por padrao, nenhuma geracao automatica ate o usuario ativar.
        // Quando ativo e com repeticao, agenda outra origem somente DEPOIS
        // que a cadeia anterior terminar. Haunted manual funciona sempre.
        // A leitura dos valores vem do painel nativo do NeoModLoader.
        private const string OriginConfigGroup = "Origins";
        private const string CfgRandomEnabled = "EnableRandomOrigins";
        private const string CfgRepeat = "RepeatAutomaticOrigins";
        private const string CfgTestMode = "TestMode";
        private const string CfgChance = "ChancePerAttemptPercent";
        private const string CfgInterval = "AttemptIntervalSeconds";
        private static readonly HashSet<string> EligibleOriginAssets =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "human", "elf", "dwarf", "orc" };

        private struct OriginOptions
        {
            public bool Enabled;
            public bool Repeat;
            public bool TestMode;
            public float Chance;
            public float Interval;
        }

        private bool _autoOriginDoneInWorld;
        private int _autoOriginCountInWorld;
        private bool _originScheduled;
        private bool _lastOriginsEnabled;
        private bool _lastOriginsRepeat;
        private bool _lastOriginsTestMode;
        private float _lastOriginChance = -1f;
        private float _lastOriginInterval = -1f;
        private float _nextOriginAttemptAt;
        private float _nextNoEligibleWarningAt;
        private bool _originLimitWarned;
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

        // Perseguicao adaptativa: consulta de posicao mais precisa quando o alvo
        // esta perto, sem varrer TODAS as unidades mais de uma vez a cada 2s.
        // Recuperacao de impasses usa troca de hospedeiro, nao altera
        // pathfinding global e nunca teleporta diretamente uma unidade.
        private const string PursuitConfigGroup = "PursuitAI";
        private const string CfgAdaptive = "EnableAdaptiveTracking";
        private const string CfgAntiDistraction = "OverrideNormalBehaviors";
        private const string CfgRescue = "EnableStuckRescue";
        private const string CfgStuckSeconds = "StuckThresholdSeconds";
        private const string CfgPursuitLogs = "VerbosePursuitLogs";
        private struct PursuitOptions
        {
            public bool Adaptive;
            public bool OverrideBehaviors;
            public bool Rescue;
            public float StuckSeconds;
            public bool Verbose;
        }
        private float _nextMotionTickAt;
        private float _nextMoveCommandAt;
        private float _nextBehaviorOverrideAt;
        private float _lastSignificantProgressAt;
        private float _bestTrackedDistance;
        private float _nextRescueWarningAt;
        private bool _pursuitOptionsWarned;
        private Actor _trackedTarget;
        private Actor _trackedHost;

        // NO MORE GAMES: o jogador que fica circulando um perseguidor muito
        // proximo por bastante tempo provoca uma corrida sobrenatural.
        // Exige caminho percorrido + baixa eficiencia de fuga, nao apenas tempo.
        private const string AntiKiteConfigGroup = "AntiKite";
        private const string CfgAntiKiteEnabled = "EnableAntiKite";
        private const string CfgAntiKiteRadius = "AntiKiteRadius";
        private const string CfgAntiKiteSeconds = "AntiKiteSeconds";
        private const string CfgAntiKiteDuration = "AntiKiteBoostDuration";
        private const string CfgAntiKiteCooldown = "AntiKiteCooldown";
        private const string CfgAntiKiteVerbose = "VerboseAntiKiteLogs";
        private struct AntiKiteOptions
        {
            public bool Enabled;
            public float Radius;
            public float Seconds;
            public float Duration;
            public float Cooldown;
            public bool Verbose;
        }
        private struct KiteSample
        {
            public float Time;
            public Vector2 Position;
            public KiteSample(float time, Vector2 position) { Time = time; Position = position; }
        }
        private readonly List<KiteSample> _kiteSamples = new List<KiteSample>();
        private AntiKiteOptions _antiKiteOptions = new AntiKiteOptions
        {
            Enabled = true, Radius = 15f, Seconds = 30f, Duration = 6f, Cooldown = 90f, Verbose = false
        };
        private Actor _kiteHost;
        private Actor _kiteTarget;
        private float _kiteNearSince = -1f;
        private float _kiteAwaySince = -1f;
        private float _kiteNextSampleAt;
        private float _kiteCooldownUntil;
        private float _kiteBoostUntil;
        private bool _kiteBoostActive;
        private bool _kiteSlowRemoved;
        private bool _kiteFastRemoved;
        private bool _kiteCleanupWarned;
        private bool _antiKiteConfigWarned;
        private float _nextKiteDebugLogAt;


        // The Witness: cobre ambas as variantes (Haunted e Haunted: Trauma).
        // Nao adivinha o autor de uma morte por mera proximidade.
        // Cruza (1) uma vitima HAUNTED que realmente morreu, (2) aumento
        // exatamente de 1 no contador nativo de abates e (3) proximidade.
        // SEM hook de dano confirmado, isso e um CANDIDATO, nao certeza absoluta.
        // Fail closed: sem dados, multiplos candidatos ou incerteza -> nao infectar.
        private const string WitnessConfigGroup = "TheWitness";
        private const string CfgWitnessTracking = "EnableWitnessTracking";
        private const string CfgWitnessTransfer = "EnableKillerTransfer";
        private const string CfgWitnessMaxDistance = "MaxKillerDistance";
        private const string CfgWitnessVerbose = "VerboseWitnessLogs";
        private struct WitnessOptions
        {
            public bool Tracking;
            public bool Transfer;
            public float MaxDistance;
            public bool Verbose;
        }
        private readonly Dictionary<Actor, int> _previousKills = new Dictionary<Actor, int>();
        private readonly Dictionary<Actor, Vector2> _lastHauntedPositions = new Dictionary<Actor, Vector2>();
        private bool _witnessConfigWarned;
        private bool _witnessCountersWarned;
        private int _witnessDeaths;
        private int _witnessConfirmedCandidates;
        private int _witnessTransfers;


        // TRAUMA WITNESS: morte violenta observada por proximidade (NAO raycast de visao).
        // Observacao e transferencia separadas: ambas exigem config valida.
        // Uma testemunha elegivel por evento, cooldown global e nenhuma infeccao
        // por morte natural ou pelo proprio mod. Causa da morte e INFERIDA,
        // nunca declarada como fato incontroverso.
        private const string TraumaConfigGroup = "TraumaWitness";
        private const string CfgTraumaTrack = "TrackViolentDeaths";
        private const string CfgTraumaTransfer = "EnableTraumaTransfer";
        private const string CfgTraumaOrdinary = "IncludeNonHauntedDeaths";
        private const string CfgTraumaRadius = "TraumaRadius";
        private const string CfgTraumaChance = "TraumaChancePercent";
        private const string CfgTraumaCooldown = "TraumaCooldownSeconds";
        private const string CfgTraumaVerbose = "VerboseTraumaLogs";
        private const int MaxTraumaObservedActors = 6500;
        private const float MaxLikelyKillerDistance = 8f;
        private struct TraumaOptions
        {
            public bool Tracking;
            public bool Transfer;
            public bool OrdinaryDeaths;
            public float Radius;
            public float Chance;
            public float Cooldown;
            public bool Verbose;
        }
        private struct TraumaRecord
        {
            public Vector2 Position;
            public bool Burning;
            public bool Haunted;
            public TraumaRecord(Vector2 position, bool burning, bool haunted)
            {
                Position = position;
                Burning = burning;
                Haunted = haunted;
            }
        }
        private readonly Dictionary<Actor, TraumaRecord> _traumaPreviouslyAlive =
            new Dictionary<Actor, TraumaRecord>();
        private readonly Dictionary<Actor, int> _traumaPreviousKills =
            new Dictionary<Actor, int>();
        private readonly Dictionary<Actor, int> _traumaExposures =
            new Dictionary<Actor, int>();
        private readonly HashSet<Actor> _entityExecutedVictims = new HashSet<Actor>();
        private readonly HashSet<Actor> _witnessTransferredVictims = new HashSet<Actor>();
        private bool _traumaConfigWarned;
        private bool _traumaPopulationWarned;
        private bool _traumaStatusWarned;
        private float _traumaLastTransferAt = -100000f;
        private int _traumaObservedDeaths;
        private int _traumaViolentDeaths;
        private int _traumaTransfers;

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
            // A origem traumatica e distinguivel na ficha da criatura,
            // mas continua a acionar TODO o sistema de perseguicao e Witness.
            AssetManager.traits.add(new ActorTrait
            {
                id = TraumaTraitId,
                group_id = GroupId,
                path_icon = "ui/icons/neomodloader",
                can_be_given = false,
                can_be_removed = true,
                can_be_cured = false,
                needs_to_be_explored = false
            });
            // Trait privada de velocidade extrema. Nao e um poder editavel;
            // o mod a adiciona e remove apenas durante o castigo de proximidade.
            ActorTrait rush = new ActorTrait
            {
                id = AntiKiteBoostTraitId,
                group_id = GroupId,
                path_icon = "ui/icons/neomodloader",
                can_be_given = false,
                can_be_removed = false,
                can_be_cured = false,
                needs_to_be_explored = false
            };
            AssetManager.traits.add(rush);
            rush.base_stats["speed"] = PunishingRushSpeedBonus;
            LogInfo("THE UNRELENTING: TRAIT HAUNTED REGISTRADA COM SUCESSO! ID=" + TraitId);
            LogInfo("THE UNRELENTING: TRAIT HAUNTED: TRAUMA REGISTRADA! ID=" + TraumaTraitId);
            LogInfo("THE UNRELENTING: V0.11.4 TRAUMA TRAIT + TRAUMA WITNESS + ANTI-KITE + PURSUIT + WITNESS + ORIGINS ATIVOS.");
            LogInfo("THE UNRELENTING: ANTI-KITE PREPARADO! BONUS_SPEED=" + PunishingRushSpeedBonus.ToString("F0") +
                " RAIO_DEFAULT=15 TEMPO_DEFAULT=30s; CONFIGURAVEL NO NML.");
            LogInfo("THE UNRELENTING: WITNESS: rastreio de mortes ativo; transmissao a assassino PROVAVEL desligada por padrao.");
            LogInfo("THE UNRELENTING: TRAUMA: observacao de mortes violentas e transferencia configuraveis no NML; INFECCAO DESLIGADA POR PADRAO.");
            LogInfo("THE UNRELENTING: PURSUIT AI: precisao por proximidade e resgate por possessao configuraveis.");
            LogInfo("THE UNRELENTING: ORIGINS: menu do NML > Mods > The Unrelenting > Configuracao.");
            LogInfo("THE UNRELENTING: ORIGINS: aleatoria DESLIGADA por padrao; Haunted manual sempre disponivel.");
            LogInfo("THE UNRELENTING: CHANCES DE VELOCIDADE LENTO=" + SlowChancePercent +
                "% NORMAL=" + NormalChancePercent + "% RAPIDO=" +
                (100 - SlowChancePercent - NormalChancePercent) + "%.");
            LogInfo("THE UNRELENTING: ARRANCADAS CONFIGURADAS! ATIVO=" + EnableSuddenBurst +
                " CHANCE_POR_TENTATIVA=" + BurstChancePercent + "% DURACAO=3-5.5s REAL.");
        }

        // Centralizar o teste e importante: todas as origens compartilham
        // o mesmo perseguidor, fila, protecoes e logica de morte.
        private static bool HasAnyHaunted(Actor actor)
        {
            return actor != null && (actor.hasTrait(TraitId) || actor.hasTrait(TraumaTraitId));
        }

        private static string GetHauntedKind(Actor actor)
        {
            if (actor == null) return "NENHUM";
            if (actor.hasTrait(TraumaTraitId)) return "TRAUMA";
            if (actor.hasTrait(TraitId)) return "NORMAL";
            return "NENHUM";
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
                Repeat = true,
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
                // Compatibilidade com arquivo de configuracao gravado pela v0.10.1.
                // Se a nova opcao nao existir nele, nao desligar Origins inteiro.
                try { options.Repeat = (bool)group[CfgRepeat].GetValue(); }
                catch { options.Repeat = true; }
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

        private WitnessOptions ReadWitnessOptions()
        {
            WitnessOptions options = new WitnessOptions
            {
                Tracking = true,
                Transfer = false,
                MaxDistance = 8f,
                Verbose = false
            };
            try
            {
                ModConfig config = GetConfig();
                if (config == null) return options;
                var group = config[WitnessConfigGroup];
                options.Tracking = (bool)group[CfgWitnessTracking].GetValue();
                options.Transfer = (bool)group[CfgWitnessTransfer].GetValue();
                options.MaxDistance = Mathf.Clamp(Convert.ToSingle(group[CfgWitnessMaxDistance].GetValue()), 2f, 15f);
                options.Verbose = (bool)group[CfgWitnessVerbose].GetValue();
                _witnessConfigWarned = false;
            }
            catch (Exception e)
            {
                // Falla segura: se o painel estiver corrompido, nao contagiar.
                options.Transfer = false;
                if (!_witnessConfigWarned)
                {
                    LogWarning("THE UNRELENTING: WITNESS CONFIG INDISPONIVEL! TRANSMISSAO DESATIVADA. " +
                        e.GetType().Name + ": " + e.Message);
                    _witnessConfigWarned = true;
                }
            }
            return options;
        }

        private TraumaOptions ReadTraumaOptions()
        {
            TraumaOptions opts = new TraumaOptions
            {
                Tracking = true, Transfer = false, OrdinaryDeaths = true,
                Radius = 10f, Chance = 25f, Cooldown = 120f, Verbose = false
            };
            try
            {
                ModConfig config = GetConfig();
                if (config == null) { opts.Transfer = false; return opts; }
                var group = config[TraumaConfigGroup];
                opts.Tracking = (bool)group[CfgTraumaTrack].GetValue();
                opts.Transfer = (bool)group[CfgTraumaTransfer].GetValue();
                opts.OrdinaryDeaths = (bool)group[CfgTraumaOrdinary].GetValue();
                opts.Radius = Mathf.Clamp(Convert.ToSingle(group[CfgTraumaRadius].GetValue()), 3f, 30f);
                opts.Chance = Mathf.Clamp(Convert.ToSingle(group[CfgTraumaChance].GetValue()), 0f, 100f);
                opts.Cooldown = Mathf.Clamp(Convert.ToSingle(group[CfgTraumaCooldown].GetValue()), 10f, 900f);
                opts.Verbose = (bool)group[CfgTraumaVerbose].GetValue();
                _traumaConfigWarned = false;
            }
            catch (Exception e)
            {
                opts.Tracking = false;
                opts.Transfer = false; // Nao contagiar com configuracao ausente/corrompida.
                if (!_traumaConfigWarned)
                {
                    LogWarning("THE UNRELENTING: TRAUMA CONFIG INDISPONIVEL! TRANSFERENCIA DESATIVADA. " +
                        e.GetType().Name + ": " + e.Message);
                    _traumaConfigWarned = true;
                }
            }
            return opts;
        }

        private PursuitOptions ReadPursuitOptions()
        {
            PursuitOptions opts = new PursuitOptions
            {
                Adaptive = true,
                OverrideBehaviors = true,
                Rescue = true,
                StuckSeconds = 18f,
                Verbose = false
            };
            try
            {
                ModConfig config = GetConfig();
                if (config == null) return opts;
                var group = config[PursuitConfigGroup];
                opts.Adaptive = (bool)group[CfgAdaptive].GetValue();
                opts.OverrideBehaviors = (bool)group[CfgAntiDistraction].GetValue();
                opts.Rescue = (bool)group[CfgRescue].GetValue();
                opts.StuckSeconds = Mathf.Clamp(Convert.ToSingle(group[CfgStuckSeconds].GetValue()), 10f, 90f);
                opts.Verbose = (bool)group[CfgPursuitLogs].GetValue();
                _pursuitOptionsWarned = false;
            }
            catch (Exception e)
            {
                // Usar defaults seguros sem impedir o funcionamento das versoes anteriores.
                if (!_pursuitOptionsWarned)
                {
                    LogWarning("THE UNRELENTING: PURSUIT CONFIG INDISPONIVEL; usando defaults: " +
                        e.GetType().Name + ": " + e.Message);
                    _pursuitOptionsWarned = true;
                }
            }
            return opts;
        }

        private AntiKiteOptions ReadAntiKiteOptions()
        {
            AntiKiteOptions result = new AntiKiteOptions
            {
                Enabled = true, Radius = 15f, Seconds = 30f, Duration = 6f,
                Cooldown = 90f, Verbose = false
            };
            try
            {
                ModConfig cfg = GetConfig();
                if (cfg == null) return result;
                var group = cfg[AntiKiteConfigGroup];
                result.Enabled = (bool)group[CfgAntiKiteEnabled].GetValue();
                result.Radius = Mathf.Clamp(Convert.ToSingle(group[CfgAntiKiteRadius].GetValue()), 5f, 30f);
                result.Seconds = Mathf.Clamp(Convert.ToSingle(group[CfgAntiKiteSeconds].GetValue()), 5f, 120f);
                result.Duration = Mathf.Clamp(Convert.ToSingle(group[CfgAntiKiteDuration].GetValue()), 2f, 15f);
                result.Cooldown = Mathf.Clamp(Convert.ToSingle(group[CfgAntiKiteCooldown].GetValue()), 20f, 240f);
                result.Verbose = (bool)group[CfgAntiKiteVerbose].GetValue();
                _antiKiteConfigWarned = false;
            }
            catch (Exception e)
            {
                if (!_antiKiteConfigWarned)
                {
                    LogWarning("THE UNRELENTING: ANTI-KITE CONFIG INDISPONIVEL; defaults usados. " +
                        e.GetType().Name + ": " + e.Message);
                    _antiKiteConfigWarned = true;
                }
            }
            return result;
        }

        private void ClearKiteObservation(bool forgetIdentity)
        {
            _kiteSamples.Clear();
            _kiteNearSince = -1f;
            _kiteAwaySince = -1f;
            _kiteNextSampleAt = 0f;
            _nextKiteDebugLogAt = 0f;
            if (forgetIdentity) { _kiteHost = null; _kiteTarget = null; }
        }

        private void ResetKiteForNewHostOrTarget()
        {
            // CleanupTemporaryTraits do hospedeiro anterior remove o bonus.
            _kiteBoostActive = false;
            _kiteSlowRemoved = false;
            _kiteFastRemoved = false;
            _kiteBoostUntil = 0f;
            _kiteCleanupWarned = false;
            // Uma troca de corpo/target nao deve herdar progresso de provocacao.
            _kiteCooldownUntil = 0f;
            ClearKiteObservation(true);
        }

        private void StartPunishingRush(float now)
        {
            if (_kiteBoostActive || !Usable(_host) || !Usable(_target) || _burstActive) return;
            bool removedSlow = false;
            bool removedFast = false;
            if (_currentSpeed == PursuitSpeed.Slow)
            {
                if (!RemoveOwnedSpeedTrait(_host, "slow")) return;
                removedSlow = true;
            }
            else if (_currentSpeed == PursuitSpeed.Fast)
            {
                if (!RemoveOwnedSpeedTrait(_host, "fast")) return;
                removedFast = true;
            }
            if (!AddOwnedSpeedTrait(_host, AntiKiteBoostTraitId))
            {
                if (removedSlow) AddOwnedSpeedTrait(_host, "slow");
                if (removedFast) AddOwnedSpeedTrait(_host, "fast");
                LogWarning("THE UNRELENTING: ANTI-KITE NAO CONSEGUIU APLICAR BOOST; estado restaurado.");
                return;
            }
            _kiteSlowRemoved = removedSlow;
            _kiteFastRemoved = removedFast;
            _kiteBoostActive = true;
            _kiteCleanupWarned = false;
            _kiteBoostUntil = now + _antiKiteOptions.Duration;
            _kiteCooldownUntil = _kiteBoostUntil + _antiKiteOptions.Cooldown;
            // Aumentar a frequencia de comandos durante a arrancada.
            _nextMoveCommandAt = 0f;
            _nextBehaviorOverrideAt = now;
            LogInfo("THE UNRELENTING: ANTI-KITE JUMPSCARE INICIADO! " +
                "HOSPEDEIRO_TEMP_ID=" + _host.GetHashCode() +
                " ALVO_TEMP_ID=" + _target.GetHashCode() +
                " SPEED_BONUS=" + PunishingRushSpeedBonus.ToString("F0") +
                " DURACAO_REAL=" + _antiKiteOptions.Duration.ToString("F1") + "s");
            ClearKiteObservation(false);
        }

        private void EndPunishingRush()
        {
            if (!_kiteBoostActive) return;
            if (!Usable(_host))
            {
                // Remocao fica para o cleanup do host e troca de mundo.
                _kiteBoostActive = false;
                ClearKiteObservation(false);
                return;
            }
            if (!RemoveOwnedSpeedTrait(_host, AntiKiteBoostTraitId))
            {
                if (!_kiteCleanupWarned)
                {
                    LogWarning("THE UNRELENTING: ANTI-KITE FALHOU AO LIMPAR BONUS; " +
                        "nova tentativa sera feita.");
                    _kiteCleanupWarned = true;
                }
                return;
            }
            if (_kiteSlowRemoved && !AddOwnedSpeedTrait(_host, "slow"))
                LogWarning("THE UNRELENTING: ANTI-KITE FALHA AO RESTAURAR SLOW.");
            if (_kiteFastRemoved && !AddOwnedSpeedTrait(_host, "fast"))
                LogWarning("THE UNRELENTING: ANTI-KITE FALHA AO RESTAURAR FAST.");
            _kiteBoostActive = false;
            _kiteSlowRemoved = false;
            _kiteFastRemoved = false;
            _kiteCleanupWarned = false;
            ClearKiteObservation(false);
            LogInfo("THE UNRELENTING: ANTI-KITE JUMPSCARE ENCERRADO! " +
                "HOSPEDEIRO_TEMP_ID=" + _host.GetHashCode() +
                " VELOCIDADE_RESTAURADA=" + _currentSpeed.ToString().ToUpperInvariant());
        }

        private void UpdateAntiKite(float now, float distance)
        {
            if (_kiteBoostActive)
            {
                if (now >= _kiteBoostUntil) EndPunishingRush();
                return;
            }
            if (!_antiKiteOptions.Enabled ||
                !Usable(_host) || !Usable(_target))
            {
                ClearKiteObservation(false);
                return;
            }
            if (!object.ReferenceEquals(_kiteHost, _host) ||
                !object.ReferenceEquals(_kiteTarget, _target))
            {
                ClearKiteObservation(false);
                _kiteHost = _host;
                _kiteTarget = _target;
            }
            if (now < _kiteCooldownUntil) return;
            if (distance > _antiKiteOptions.Radius)
            {
                if (_kiteAwaySince < 0f) _kiteAwaySince = now;
                if (now - _kiteAwaySince > 2f) ClearKiteObservation(false);
                return;
            }
            _kiteAwaySince = -1f;
            if (_kiteNearSince < 0f) _kiteNearSince = now;
            if (now < _kiteNextSampleAt) return;
            _kiteNextSampleAt = now + 0.55f;

            Vector2 pos;
            if (!TryGetPosition(_target, out pos)) return;
            _kiteSamples.Add(new KiteSample(now, pos));
            // Janela deslizante de 12s para distinguir um movimento circular
            // da fuga eficiente em linha reta. Nunca analisar o mapa todo aqui.
            while (_kiteSamples.Count > 1 &&
                   _kiteSamples[0].Time < now - 12f) _kiteSamples.RemoveAt(0);
            if (_kiteSamples.Count < 4) return;
            float path = 0f;
            for (int i = 1; i < _kiteSamples.Count; i++)
                path += Vector2.Distance(_kiteSamples[i - 1].Position, _kiteSamples[i].Position);
            float displacement = Vector2.Distance(_kiteSamples[0].Position,
                _kiteSamples[_kiteSamples.Count - 1].Position);
            float proximity = now - _kiteNearSince;
            // Exigir movimentacao + ineficiencia de deslocamento. Quem esta
            // correndo para LONGE tende a ganhar distancia e deslocamento liquido.
            bool circling = path >= 12f && displacement <= path * 0.78f;
            if (_antiKiteOptions.Verbose && now >= _nextKiteDebugLogAt)
            {
                _nextKiteDebugLogAt = now + 5f;
                LogInfo("THE UNRELENTING: ANTI-KITE OBSERVANDO! " +
                    "ALVO_TEMP_ID=" + _target.GetHashCode() +
                    " DISTANCIA=" + distance.ToString("F1") +
                    " PERTO_SEG=" + proximity.ToString("F1") +
                    " CAMINHO_12S=" + path.ToString("F1") +
                    " DESLOCAMENTO_12S=" + displacement.ToString("F1") +
                    " CIRCULANDO=" + circling);
            }
            if (proximity >= _antiKiteOptions.Seconds && circling && !_burstActive)
            {
                LogInfo("THE UNRELENTING: ANTI-KITE PROVOCACAO DETECTADA! " +
                    "HOSPEDEIRO_TEMP_ID=" + _host.GetHashCode() +
                    " ALVO_TEMP_ID=" + _target.GetHashCode() +
                    " PERTO_SEG=" + proximity.ToString("F1") +
                    " DISTANCIA=" + distance.ToString("F1") +
                    " CAMINHO=" + path.ToString("F1") +
                    " DESLOCAMENTO=" + displacement.ToString("F1"));
                StartPunishingRush(now);
            }
        }

        // APIs de dados do jogo mudam entre builds. Evitamos assumir a
        // existencia publica de Actor.data ou ActorData.kills na compilacao.
        private static object ReadMember(object source, string name)
        {
            if (source == null) return null;
            Type type = source.GetType();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            FieldInfo field = type.GetField(name, flags);
            if (field != null) return field.GetValue(source);
            PropertyInfo prop = type.GetProperty(name, flags);
            if (prop != null && prop.GetIndexParameters().Length == 0)
                return prop.GetValue(source, null);
            return null;
        }

        private static bool TryGetKillCount(Actor actor, out int kills)
        {
            kills = 0;
            if (actor == null) return false;
            try
            {
                object data = ReadMember(actor, "data");
                if (data == null)
                {
                    MethodInfo getData = actor.GetType().GetMethod("getData",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                        null, Type.EmptyTypes, null);
                    if (getData != null) data = getData.Invoke(actor, null);
                }
                object value = ReadMember(data, "kills");
                if (value == null) return false;
                kills = Convert.ToInt32(value);
                return true;
            }
            catch { return false; }
        }

        private static bool TryGetPosition(Actor actor, out Vector2 position)
        {
            position = Vector2.zero;
            if (actor == null || actor.current_tile == null) return false;
            try
            {
                object tile = actor.current_tile;
                object x = ReadMember(tile, "x");
                object y = ReadMember(tile, "y");
                if (x == null || y == null) return false;
                position = new Vector2(Convert.ToSingle(x), Convert.ToSingle(y));
                return true;
            }
            catch { return false; }
        }

        // Consome APENAS o conjunto de mortes Haunted que vimos em um scan anterior.
        // Mortes feitas pela propria entidade nao entram aqui: a execucao limpa
        // _knownHaunted e a fila no mesmo instante, antes do scan seguinte.
        private void ObserveWitnessDeaths(List<Actor> alive, HashSet<Actor> current, WitnessOptions options)
        {
            if (!options.Tracking) return;
            foreach (Actor old in _knownHaunted)
            {
                if (old == null || current.Contains(old) || old.isAlive()) continue;
                _witnessDeaths++;
                int victimId = old.GetHashCode();
                Vector2 position;
                if (!_lastHauntedPositions.TryGetValue(old, out position))
                {
                    LogInfo("THE UNRELENTING: WITNESS MORTE HAUNTED! VITIMA_TEMP_ID=" + victimId +
                        " SEM_POSICAO_ANTERIOR; AUTOR_NAO_IDENTIFICADO.");
                    continue;
                }

                Actor candidate = null;
                int count = 0;
                int changedCounters = 0;
                foreach (Actor actor in alive)
                {
                    if (object.ReferenceEquals(actor, old) || object.ReferenceEquals(actor, _host) ||
                        current.Contains(actor)) continue;
                    int prior, now;
                    if (!_previousKills.TryGetValue(actor, out prior) ||
                        !TryGetKillCount(actor, out now)) continue;
                    int delta = now - prior;
                    if (delta <= 0) continue;
                    changedCounters++;
                    // Contar mais de um abate por scan nao permite ligar o
                    // assassino a uma vitima especifica sem eventos de dano.
                    if (delta != 1) continue;
                    Vector2 pos;
                    if (!TryGetPosition(actor, out pos)) continue;
                    if (Vector2.Distance(pos, position) > options.MaxDistance) continue;
                    count++;
                    candidate = actor;
                }

                if (count != 1)
                {
                    LogInfo("THE UNRELENTING: WITNESS MORTE HAUNTED SEM AUTOR SEGURO! " +
                        "VITIMA_TEMP_ID=" + victimId + " CANDIDATOS_PROXIMOS=" + count +
                        " CONTADORES_ALTERADOS=" + changedCounters + " MOTIVO=" +
                        (count > 1 ? "AMBIGUO" : "SEM_CANDIDATO") +
                        " (NAO TRANSMITIU)");
                    continue;
                }

                _witnessConfirmedCandidates++;
                LogInfo("THE UNRELENTING: WITNESS ASSASSINO PROVAVEL! " +
                    "VITIMA_TEMP_ID=" + victimId + " CANDIDATO_TEMP_ID=" + candidate.GetHashCode() +
                    " ESPECIE=" + GetAssetId(candidate) + " PROVA=KILLS_DELTA_1_E_PROXIMIDADE " +
                    "TRANSFERIR=" + options.Transfer);
                if (!options.Transfer) continue;
                // Nao aceitar hospedeiro, vitima marcada ou candidato falecido.
                if (!Usable(candidate) || current.Contains(candidate) || HasAnyHaunted(candidate) ||
                    object.ReferenceEquals(candidate, _host)) continue;
                if (MutateTrait(candidate, "addTrait", TraitId) && candidate.hasTrait(TraitId))
                {
                    current.Add(candidate);
                    _witnessTransferredVictims.Add(old);
                    _witnessTransfers++;
                    LogInfo("THE UNRELENTING: WITNESS TRANSMISSAO AO ASSASSINO PROVAVEL! " +
                        "VITIMA_TEMP_ID=" + victimId + " NOVO_HAUNTED_TEMP_ID=" +
                        candidate.GetHashCode() + " TOTAL_TRANSMISSOES=" + _witnessTransfers);
                }
                else
                {
                    LogWarning("THE UNRELENTING: WITNESS FALHA AO TRANSMITIR! " +
                        "CANDIDATO_TEMP_ID=" + candidate.GetHashCode());
                }
            }
        }

        // Atualiza snapshots apos a analise. Nunca associa abates de scans
        // passados a mortes futuras; reinicia inteiramente ao mudar de mundo.
        private void SnapshotWitness(List<Actor> alive, HashSet<Actor> current, WitnessOptions options)
        {
            if (!options.Tracking)
            {
                _previousKills.Clear();
                _lastHauntedPositions.Clear();
                return;
            }
            Dictionary<Actor, int> latest = new Dictionary<Actor, int>();
            foreach (Actor actor in alive)
            {
                int kills;
                if (TryGetKillCount(actor, out kills)) latest[actor] = kills;
            }
            if (latest.Count == 0 && alive.Count > 0 && !_witnessCountersWarned)
            {
                LogWarning("THE UNRELENTING: WITNESS CONTADOR DE ABATES INACESSIVEL! " +
                    "SEM ATRIBUICAO DE ASSASSINOS NESTA BUILD.");
                _witnessCountersWarned = true;
            }
            _previousKills.Clear();
            foreach (KeyValuePair<Actor, int> pair in latest) _previousKills[pair.Key] = pair.Value;
            _lastHauntedPositions.Clear();
            foreach (Actor actor in current)
            {
                Vector2 pos;
                if (TryGetPosition(actor, out pos)) _lastHauntedPositions[actor] = pos;
            }
            if (options.Verbose && current.Count > 0)
                LogInfo("THE UNRELENTING: WITNESS SNAPSHOT! HAUNTED=" + current.Count +
                    " CONTADORES_DISPONIVEIS=" + latest.Count + " MORTES=" + _witnessDeaths +
                    " CANDIDATOS=" + _witnessConfirmedCandidates + " TRANSMISSOES=" + _witnessTransfers);
        }

        // Somente aceitamos status de fogo SE um metodo/campo runtime real o confirmar.
        // Nunca deduzimos combustao apenas pela especie ou proximidade de lava.
        private static bool TryWasBurning(Actor actor)
        {
            if (actor == null) return false;
            try
            {
                object value = ReadMember(actor, "is_on_fire") ??
                    ReadMember(actor, "isOnFire") ?? ReadMember(actor, "is_burning");
                if (value is bool && (bool)value) return true;
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                foreach (string name in new string[] { "isOnFire", "isBurning" })
                {
                    MethodInfo method = actor.GetType().GetMethod(name, flags, null, Type.EmptyTypes, null);
                    if (method != null && method.ReturnType == typeof(bool) &&
                        (bool)method.Invoke(actor, null)) return true;
                }
                foreach (string methodName in new string[] { "hasStatus", "hasStatusEffect", "hasStatusEffectById" })
                {
                    MethodInfo method = actor.GetType().GetMethod(methodName, flags, null,
                        new Type[] { typeof(string) }, null);
                    if (method == null || method.ReturnType != typeof(bool)) continue;
                    foreach (string effect in new string[] { "burning", "on_fire" })
                        if ((bool)method.Invoke(actor, new object[] { effect })) return true;
                }
            }
            catch { /* status de fogo desconhecido nesta build: nao supor que estava queimando */ }
            return false;
        }

        private void ObserveTraumaDeaths(List<Actor> alive, HashSet<Actor> current, TraumaOptions opts)
        {
            if (!opts.Tracking) return;
            if (_traumaPreviouslyAlive.Count == 0) return;
            HashSet<Actor> stillAlive = new HashSet<Actor>(alive);
            // Indicios de violencia: exatamente um ator vivo aumentou em 1 seu
            // contador de kills desde o scan anterior, PERTO da morte; ou o
            // status de queimando foi verificado antes de a vitima morrer.
            // Nao ha garantia absoluta de quem matou ou do campo de visao.
            List<Actor> possibleKillers = new List<Actor>();
            foreach (Actor actor in alive)
            {
                int previous, latest;
                if (!_traumaPreviousKills.TryGetValue(actor, out previous) ||
                    !TryGetKillCount(actor, out latest)) continue;
                if (latest - previous == 1) possibleKillers.Add(actor);
            }
            foreach (KeyValuePair<Actor, TraumaRecord> previous in _traumaPreviouslyAlive)
            {
                Actor victim = previous.Key;
                if (victim == null || stillAlive.Contains(victim) || victim.isAlive()) continue;
                if (_entityExecutedVictims.Contains(victim)) continue;
                if (_witnessTransferredVictims.Contains(victim))
                {
                    if (opts.Verbose)
                        LogInfo("THE UNRELENTING: TRAUMA IGNORADO! MOTIVO=ASSASSINO_JA_RECEBEU_HAUNTED VITIMA=" + victim.GetHashCode());
                    continue; // prioridade absoluta: assassino no The Witness.
                }
                TraumaRecord record = previous.Value;
                if (!opts.OrdinaryDeaths && !record.Haunted) continue;
                _traumaObservedDeaths++;
                Actor probableKiller = null;
                int candidates = 0;
                foreach (Actor attacker in possibleKillers)
                {
                    Vector2 pos;
                    if (!TryGetPosition(attacker, out pos)) continue;
                    if (Vector2.Distance(pos, record.Position) > MaxLikelyKillerDistance) continue;
                    probableKiller = attacker;
                    candidates++;
                }
                bool likelyMurder = candidates == 1;
                bool fireEvidence = record.Burning;
                // Nenhuma morte natural, de velhice ou sem indicio conhecido
                // pode iniciar Trauma. Multiplos possiveis assassinos = ambiguidade.
                if (!fireEvidence && !likelyMurder)
                {
                    if (opts.Verbose)
                        LogInfo("THE UNRELENTING: TRAUMA MORTE IGNORADA! VITIMA_TEMP_ID=" +
                            victim.GetHashCode() + " MOTIVO=" + (candidates > 1 ? "AUTORES_AMBIGUOS" : "SEM_INDICIO_VIOLENTO"));
                    continue;
                }
                _traumaViolentDeaths++;
                Actor witness = null;
                int eligibleCount = 0;
                foreach (Actor bystander in alive)
                {
                    if (object.ReferenceEquals(bystander, probableKiller) ||
                        object.ReferenceEquals(bystander, _host) || current.Contains(bystander) ||
                        !EligibleOriginAssets.Contains(GetAssetId(bystander))) continue;
                    Vector2 pos;
                    if (!TryGetPosition(bystander, out pos)) continue;
                    if (Vector2.Distance(pos, record.Position) > opts.Radius) continue;
                    eligibleCount++;
                    // Escolhe exatamente UM entre as testemunhas elegiveis.
                    if (_random.Next(eligibleCount) == 0) witness = bystander;
                }
                LogInfo("THE UNRELENTING: TRAUMA MORTE VIOLENTA PROVAVEL! VITIMA_TEMP_ID=" +
                    victim.GetHashCode() + " HAUNTED=" + record.Haunted +
                    " INDICIO=" + (fireEvidence ? "QUEIMANDO_ANTES_DA_MORTE" : "KILLS_DELTA_E_PROXIMIDADE") +
                    " TESTEMUNHAS_POR_PROXIMIDADE=" + eligibleCount +
                    " AUTOR_PROVAVEL=" + (probableKiller == null ? "NENHUM" : probableKiller.GetHashCode().ToString()));
                if (!opts.Transfer || witness == null) continue;
                if (Time.unscaledTime < _traumaLastTransferAt + opts.Cooldown)
                {
                    if (opts.Verbose)
                        LogInfo("THE UNRELENTING: TRAUMA COOLDOWN! RESTANTE_SEG=" +
                            (_traumaLastTransferAt + opts.Cooldown - Time.unscaledTime).ToString("F1"));
                    continue;
                }
                int exposures;
                if (!_traumaExposures.TryGetValue(witness, out exposures)) exposures = 0;
                // Testemunha que ja sobreviveu a eventos traumaticos tem chance
                // levemente maior. Nao utiliza mana nem trata Stupid como doenca mental.
                float chance = Mathf.Min(100f, opts.Chance + Mathf.Min(3, exposures) * 5f);
                float roll = (float)(_random.NextDouble() * 100.0);
                _traumaExposures[witness] = Math.Min(4, exposures + 1);
                LogInfo("THE UNRELENTING: TRAUMA TESTEMUNHA SORTEADA! VITIMA_TEMP_ID=" +
                    victim.GetHashCode() + " TESTEMUNHA_TEMP_ID=" + witness.GetHashCode() +
                    " CHANCE=" + chance.ToString("F1") + "% SORTEIO=" + roll.ToString("F1") +
                    " EXPOSICOES_ANTERIORES=" + exposures);
                if (roll >= chance) continue;
                if (!Usable(witness) || current.Contains(witness) || HasAnyHaunted(witness)) continue;
                if (MutateTrait(witness, "addTrait", TraumaTraitId) && witness.hasTrait(TraumaTraitId))
                {
                    current.Add(witness); // ja entra na fila no MESMO scan.
                    _traumaLastTransferAt = Time.unscaledTime;
                    _traumaTransfers++;
                    LogInfo("THE UNRELENTING: TRAUMA TRANSMISSAO CONFIRMADA! " +
                        "VITIMA_TEMP_ID=" + victim.GetHashCode() +
                        " NOVO_HAUNTED_TEMP_ID=" + witness.GetHashCode() +
                        " TRAIT=" + TraumaTraitId + " ORIGEM=TRAUMA" +
                        " TOTAL_TRAUMA=" + _traumaTransfers +
                        " COOLDOWN_SEG=" + opts.Cooldown.ToString("F0"));
                }
                else LogWarning("THE UNRELENTING: TRAUMA FALHA AO TRANSMITIR! TESTEMUNHA=" + witness.GetHashCode());
            }
        }

        private void SnapshotTrauma(List<Actor> alive, HashSet<Actor> current, TraumaOptions opts)
        {
            if (!opts.Tracking || alive.Count > MaxTraumaObservedActors)
            {
                _traumaPreviouslyAlive.Clear();
                _traumaPreviousKills.Clear();
                _traumaExposures.Clear();
                if (opts.Tracking && !_traumaPopulationWarned)
                {
                    LogWarning("THE UNRELENTING: TRAUMA PAUSADO: populacao acima do limite de " +
                        MaxTraumaObservedActors + " atores; nenhuma transmissao feita ate reduzir.");
                    _traumaPopulationWarned = true;
                }
                return;
            }
            _traumaPopulationWarned = false;
            _traumaPreviouslyAlive.Clear();
            _traumaPreviousKills.Clear();
            HashSet<Actor> tracked = new HashSet<Actor>();
            foreach (Actor actor in alive)
            {
                tracked.Add(actor);
                Vector2 pos;
                if (TryGetPosition(actor, out pos))
                    _traumaPreviouslyAlive[actor] = new TraumaRecord(pos, TryWasBurning(actor), current.Contains(actor));
                int kills;
                if (TryGetKillCount(actor, out kills)) _traumaPreviousKills[actor] = kills;
            }
            foreach (Actor known in new List<Actor>(_traumaExposures.Keys))
                if (!tracked.Contains(known)) _traumaExposures.Remove(known);
            // Mortes causadas internamente sao excluidas apenas no scan seguinte.
            _entityExecutedVictims.Clear();
            _witnessTransferredVictims.Clear();
            if (opts.Verbose && (_traumaObservedDeaths > 0 || _traumaTransfers > 0))
                LogInfo("THE UNRELENTING: TRAUMA RESUMO! MORTES=" + _traumaObservedDeaths +
                    " VIOLENTAS_PROVAVEIS=" + _traumaViolentDeaths + " TRANSMISSOES=" + _traumaTransfers);
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
                           options.Repeat != _lastOriginsRepeat ||
                           options.TestMode != _lastOriginsTestMode ||
                           Math.Abs(options.Chance - _lastOriginChance) > 0.001f ||
                           Math.Abs(options.Interval - _lastOriginInterval) > 0.001f;
            if (changed)
            {
                LogInfo("THE UNRELENTING: ORIGINS CONFIG ALTERADA! ALEATORIO=" + options.Enabled +
                    " REPETIR=" + options.Repeat + " TESTE_RAPIDO=" + options.TestMode +
                    " CHANCE=" + options.Chance.ToString("F2") +
                    "% INTERVALO_REAL=" + options.Interval.ToString("F0") + "s" +
                    " ORIGENS_ATE_AGORA=" + _autoOriginCountInWorld);
                _originScheduled = false;
                _lastOriginsEnabled = options.Enabled;
                _lastOriginsRepeat = options.Repeat;
                _lastOriginsTestMode = options.TestMode;
                _lastOriginChance = options.Chance;
                _lastOriginInterval = options.Interval;
            }
            if (!options.Enabled) return;
            if (!options.Repeat && _autoOriginDoneInWorld)
            {
                if (!_originLimitWarned)
                {
                    LogInfo("THE UNRELENTING: ORIGINS LIMITE ATINGIDO! REPETIR=False; " +
                        "so uma origem automatica neste mundo. Habilite Repetir para novas maldicoes.");
                    _originLimitWarned = true;
                }
                return;
            }
            _originLimitWarned = false;

            // Nunca gerar nova origem enquanto existir uma cadeia Haunted ativa.
            // Depois da ultima morte, reagendar tempo COMPLETO (inclusive no teste).
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
            LogInfo("THE UNRELENTING: ORIGINS TENTATIVA! MODO=" +
                (options.TestMode ? "TESTE" : "RARO") +
                " CHANCE=" + effectiveChance.ToString("F2") +
                "% SORTEIO=" + roll.ToString("F2") +
                " ORIGENS_ANTERIORES=" + _autoOriginCountInWorld);
            if (roll >= effectiveChance)
            {
                LogInfo("THE UNRELENTING: ORIGINS SORTEIO SEM EVENTO!");
                return;
            }

            Actor candidate = PickOriginTarget(alive, currentHaunted);
            if (candidate == null)
            {
                // Reduzir spam quando o jogador esta construindo um mundo vazio.
                if (Time.unscaledTime >= _nextNoEligibleWarningAt)
                {
                    LogWarning("THE UNRELENTING: ORIGINS SEM ELEGIVEIS! Necessita humano, elfo, anao ou orc vivo.");
                    _nextNoEligibleWarningAt = Time.unscaledTime + 60f;
                }
                return;
            }
            int candidateId = candidate.GetHashCode();
            if (MutateTrait(candidate, "addTrait", TraitId) && candidate.hasTrait(TraitId))
            {
                // Garantir que a fila seja atualizada ainda neste mesmo scan.
                currentHaunted.Add(candidate);
                _autoOriginDoneInWorld = true;
                _autoOriginCountInWorld++;
                LogInfo("THE UNRELENTING: ORIGINS HAUNTED GERADA! ALVO_TEMP_ID=" +
                    candidateId + " ESPECIE=" + GetAssetId(candidate) +
                    " MODO=" + (options.TestMode ? "TESTE" : "RARO") +
                    " TOTAL_ORIGENS_AUTOMATICAS=" + _autoOriginCountInWorld +
                    " REPETIR=" + options.Repeat);
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
            if (_kiteBoostActive) return;
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

        // Um novo alvo ou corpo reinicia o medidor de progresso.
        private void ResetPursuitProgress()
        {
            _trackedHost = _host;
            _trackedTarget = _target;
            _lastSignificantProgressAt = Time.unscaledTime;
            _bestTrackedDistance = Usable(_host) && Usable(_target) ?
                _host.distanceToActorTile(_target) : float.MaxValue;
            _nextMotionTickAt = 0f;
            _nextMoveCommandAt = 0f;
            _nextBehaviorOverrideAt = Time.unscaledTime + 3f;
        }

        private static float MotionTickSeconds(float distance, bool adaptive)
        {
            if (!adaptive) return 2f;
            if (distance > 50f) return 1.6f;
            if (distance > 15f) return 0.8f;
            if (distance > 5f) return 0.35f;
            return 0.18f;
        }

        // Resgate: outra criatura JA EXISTENTE, significativamente mais proxima
        // da vitima. Funciona para ilhas separadas se ha hospedeiro elegivel
        // disponivel do lado da vitima; caso contrario, nenhuma travessia e prometida.
        private Actor FindRescueHost(Actor currentHost, Actor target)
        {
            if (World.world == null || !Usable(currentHost) || !Usable(target)) return null;
            float oldD = currentHost.distanceToActorTile(target);
            if (oldD < 7f) return null;
            Actor chosen = null;
            Actor fallback = null;
            int candidates = 0;
            float closestScore = float.MaxValue;
            foreach (Actor actor in World.world.units)
            {
                if (actor == null || object.ReferenceEquals(actor, currentHost) ||
                    !IsAllowedHost(actor, _knownHaunted, currentHost) ||
                    HasAnyHaunted(actor)) continue;
                float distanceToTarget = actor.distanceToActorTile(target);
                // Um pouco de distancia preserva a sensacao de aproximacao.
                if (distanceToTarget < 2.5f || distanceToTarget > oldD - 4f) continue;
                // Escolher preferencialmente hospedeiros a 6-22 tiles da vitima.
                if (distanceToTarget >= 6f && distanceToTarget <= 22f)
                {
                    candidates++;
                    if (_random.Next(candidates) == 0) chosen = actor;
                }
                else
                {
                    float score = Mathf.Abs(distanceToTarget - 14f);
                    if (score < closestScore)
                    {
                        closestScore = score;
                        fallback = actor;
                    }
                }
            }
            return chosen ?? fallback;
        }

        private void TryRescueStalledPursuit(float distance, PursuitOptions opts)
        {
            if (!opts.Rescue || distance <= 5f ||
                Time.unscaledTime - _lastSignificantProgressAt < opts.StuckSeconds) return;
            Actor old = _host;
            Actor alternative = FindRescueHost(old, _target);
            if (alternative == null)
            {
                if (Time.unscaledTime >= _nextRescueWarningAt)
                {
                    LogInfo("THE UNRELENTING: PURSUIT SEM CAMINHO / SEM RESGATE DISPONIVEL! " +
                        "HOSPEDEIRO_TEMP_ID=" + old.GetHashCode() + " DISTANCIA=" +
                        distance.ToString("F1") +
                        " MOTIVO=POSSIVEL_AGUA_OBSTACULO_OU_ALVO_MOVEL; " +
                        "nenhum outro hospedeiro mais proximo disponivel.");
                    _nextRescueWarningAt = Time.unscaledTime + 25f;
                }
                // Nao bombardear a busca a cada 0.2s se faltam hospedeiros.
                _lastSignificantProgressAt = Time.unscaledTime - opts.StuckSeconds + 10f;
                return;
            }
            int oldId = old.GetHashCode();
            float newDistance = alternative.distanceToActorTile(_target);
            SetHost(alternative, true);
            LogInfo("THE UNRELENTING: PURSUIT RESGATE POR TROCA DE CORPO! " +
                "ANTERIOR_TEMP_ID=" + oldId + " NOVO_TEMP_ID=" + alternative.GetHashCode() +
                " DISTANCIA_ANTERIOR=" + distance.ToString("F1") +
                " NOVA_DISTANCIA=" + newDistance.ToString("F1") +
                " POSSIVEL_SEPARACAO_POR_AGUA=True");
        }

        // Apenas dois Actors sao avaliados frequentemente. O scan global,
        // incluindo Witness e Origins, continua limitado a 2 segundos reais.
        private void UpdatePursuitMotion()
        {
            if (World.world == null || !object.ReferenceEquals(World.world, _activeWorldReference) ||
                !Usable(_host) || !Usable(_target)) return;
            float now = Time.unscaledTime;
            if (now < _nextMotionTickAt) return;
            PursuitOptions opts = ReadPursuitOptions();
            float distance = _host.distanceToActorTile(_target);
            _nextMotionTickAt = now + MotionTickSeconds(distance, opts.Adaptive);
            if (!object.ReferenceEquals(_trackedHost, _host) ||
                !object.ReferenceEquals(_trackedTarget, _target)) ResetPursuitProgress();
            if (distance < _bestTrackedDistance - 1.0f)
            {
                _bestTrackedDistance = distance;
                _lastSignificantProgressAt = now;
            }
            UpdateSuddenBurst();
            if (distance <= ContactDistance)
            {
                int victimId = _target.GetHashCode();
                int hostId = _host.GetHashCode();
                LogInfo("THE UNRELENTING: CONTATO COM A VITIMA! ALVO_TEMP_ID=" + victimId +
                    " HOSPEDEIRO_TEMP_ID=" + hostId);
                Actor victim = _target;
                _entityExecutedVictims.Add(victim);
                victim.dieSimpleNone();
                if (!victim.isAlive())
                {
                    _killsThisChain++;
                    LogInfo("THE UNRELENTING: EXECUCAO CONFIRMADA! ALVO_TEMP_ID=" + victimId +
                        " HOSPEDEIRO_TEMP_ID=" + hostId + " TOTAL=" + _killsThisChain);
                    _targetQueue.Remove(victim);
                    _knownHaunted.Remove(victim);
                    _target = _targetQueue.Count > 0 ? _targetQueue[0] : null;
                    if (_kiteBoostActive) EndPunishingRush();
                    ResetKiteForNewHostOrTarget();
                    if (_target != null)
                    {
                        LogInfo("THE UNRELENTING: PROXIMA VITIMA! NOVO_ALVO_TEMP_ID=" +
                            _target.GetHashCode() + " HOSPEDEIRO_TEMP_ID=" + hostId +
                            " RESTANTES=" + _targetQueue.Count);
                        ResetPursuitProgress();
                    }
                    else EndEntity("CADEIA_CONCLUIDA", true);
                    return;
                }
                LogWarning("THE UNRELENTING: EXECUCAO NAO CONFIRMADA! ALVO_TEMP_ID=" + victimId);
                return;
            }

            UpdateAntiKite(now, distance);

            // Neutraliza tarefas normais (incluindo sono) periodicamente, mas
            // NAO a cada frame, evitando resetar todo o pathfinding constantemente.
            if (opts.OverrideBehaviors && now >= _nextBehaviorOverrideAt)
            {
                _host.cancelAllBeh();
                _nextBehaviorOverrideAt = now + 4.5f;
                if (opts.Verbose)
                    LogInfo("THE UNRELENTING: PURSUIT ROTINA NORMAL INTERROMPIDA! " +
                        "HOSPEDEIRO_TEMP_ID=" + _host.GetHashCode());
            }
            // Verificacoes de proximidade podem ser muito frequentes; nao
            // reiniciar a navegacao a cada 0,18s para evitar tremores.
            if (now >= _nextMoveCommandAt)
            {
                _host.goTo(_target.current_tile, false);
                _nextMoveCommandAt = now + Mathf.Max(0.35f, MotionTickSeconds(distance, opts.Adaptive));
            }
            TryRescueStalledPursuit(distance, opts);
            if (now - _lastProgressTime >= ProgressLogInterval)
            {
                LogInfo("THE UNRELENTING: PROGRESSO CADEIA ALVO_TEMP_ID=" +
                    _target.GetHashCode() + " HOSPEDEIRO_TEMP_ID=" + _host.GetHashCode() +
                    " DISTANCIA=" + distance.ToString("F1") +
                    " DISTANCIA_ANTERIOR=" + _lastDistance.ToString("F1") +
                    " RESTANTES=" + _targetQueue.Count +
                    " PROXIMA_CORRECAO_SEG=" + MotionTickSeconds(distance, opts.Adaptive).ToString("F2"));
                _lastDistance = distance;
                _lastProgressTime = now;
            }
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
            ResetKiteForNewHostOrTarget();
            _host.cancelAllBeh();
            _currentSpeed = RollSpeed();
            _burstActive = false; // antiga corrida limpa por CleanupTemporaryTraits(old)
            _burstCleanupWarned = false;
            _nextBurstCheckAt = Time.unscaledTime + RandomSeconds(FirstBurstCheckMin, FirstBurstCheckMax);
            ApplyTemporaryTraits(_host, _currentSpeed);
            _nextHostChangeTime = Time.unscaledTime + (isChange ? HostChangeInterval : FirstHostChangeAfter);
            _lastProgressTime = Time.unscaledTime;
            _lastDistance = _target != null ? _host.distanceToActorTile(_target) : 0f;
            ResetPursuitProgress();
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
                    _entityExecutedVictims.Add(old);
                    old.dieSimpleNone();
                }
                LogInfo("THE UNRELENTING: ENTIDADE ENCERRADA! MOTIVO=" + reason +
                    " ULTIMO_HOSPEDEIRO_TEMP_ID=" + old.GetHashCode() +
                    " DESAPARECEU=" + (vanish && !old.isAlive()) +
                    " EXECUCOES=" + _killsThisChain);
            }
            _host = null;
            _target = null;
            ResetKiteForNewHostOrTarget();
            _killsThisChain = 0;
            _currentSpeed = PursuitSpeed.Normal;
            _burstActive = false;
            _burstCleanupWarned = false;
            _nextBurstCheckAt = 0f;
            _warnedNoHost = false;
            _trackedHost = null;
            _trackedTarget = null;
            _nextMotionTickAt = 0f;
            _nextMoveCommandAt = 0f;
        }

        private void ResetForWorld()
        {
            // Se outro mundo foi carregado, nao chamar die() em objetos antigos.
            foreach (Actor actor in new List<Actor>(_traitsAddedByMod.Keys))
                CleanupTemporaryTraits(actor);
            _traitsAddedByMod.Clear();
            ResetKiteForNewHostOrTarget();
            _knownHaunted.Clear();
            _targetQueue.Clear();
            _host = null;
            _target = null;
            ResetKiteForNewHostOrTarget();
            _killsThisChain = 0;
            _currentSpeed = PursuitSpeed.Normal;
            _burstActive = false;
            _burstCleanupWarned = false;
            _nextBurstCheckAt = 0f;
            _warnedNoHost = false;
            _reportedError = false;
            _autoOriginDoneInWorld = false;
            _autoOriginCountInWorld = 0;
            _originScheduled = false;
            _lastOriginsEnabled = false;
            _lastOriginsRepeat = false;
            _lastOriginsTestMode = false;
            _lastOriginChance = -1f;
            _lastOriginInterval = -1f;
            _nextOriginAttemptAt = 0f;
            _nextNoEligibleWarningAt = 0f;
            _originLimitWarned = false;
            _warnedOriginSettings = false;
            _previousKills.Clear();
            _lastHauntedPositions.Clear();
            _witnessConfigWarned = false;
            _witnessCountersWarned = false;
            _witnessDeaths = 0;
            _witnessConfirmedCandidates = 0;
            _witnessTransfers = 0;
            _traumaPreviouslyAlive.Clear();
            _traumaPreviousKills.Clear();
            _traumaExposures.Clear();
            _entityExecutedVictims.Clear();
            _witnessTransferredVictims.Clear();
            _traumaConfigWarned = false;
            _traumaPopulationWarned = false;
            _traumaStatusWarned = false;
            _traumaLastTransferAt = -100000f;
            _traumaObservedDeaths = 0;
            _traumaViolentDeaths = 0;
            _traumaTransfers = 0;
            _trackedHost = null;
            _trackedTarget = null;
            _nextMotionTickAt = 0f;
            _nextMoveCommandAt = 0f;
            _nextBehaviorOverrideAt = 0f;
            _lastSignificantProgressAt = 0f;
            _bestTrackedDistance = float.MaxValue;
            _nextRescueWarningAt = 0f;
            _pursuitOptionsWarned = false;
        }

        private void Update()
        {
            _scanClock += Time.unscaledDeltaTime;
            if (_scanClock < ScanInterval)
            {
                try { UpdatePursuitMotion(); }
                catch (Exception e)
                {
                    if (!_reportedError)
                    {
                        LogWarning("THE UNRELENTING: ERRO NA PERSEGUICAO RAPIDA: " + e);
                        _reportedError = true;
                    }
                }
                return;
            }
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
                    if (HasAnyHaunted(actor)) current.Add(actor);
                }

                _antiKiteOptions = ReadAntiKiteOptions();
                if (!_antiKiteOptions.Enabled && _kiteBoostActive) EndPunishingRush();

                // The Witness usa snapshots da iteracao ANTERIOR para identificar
                // uma morte natural/combate antes de atualizar a fila.
                WitnessOptions witness = ReadWitnessOptions();
                TraumaOptions trauma = ReadTraumaOptions();
                ObserveWitnessDeaths(alive, current, witness);
                ObserveTraumaDeaths(alive, current, trauma);

                // Origins aplica Haunted NORMAL em UMA unidade por evento.
                // Novas origens recorrentes aguardam o fim da cadeia anterior.
                UpdateOrigins(alive, current);
                SnapshotWitness(alive, current, witness);
                SnapshotTrauma(alive, current, trauma);

                // A fila respeita a ordem de descoberta; nao abre novos perseguidores.
                foreach (Actor actor in alive)
                {
                    if (!current.Contains(actor) || _knownHaunted.Contains(actor)) continue;
                    _targetQueue.Add(actor);
                    LogInfo("THE UNRELENTING: HAUNTED ENTROU NA FILA! ALVO_TEMP_ID=" +
                        actor.GetHashCode() + " TIPO=" + GetHauntedKind(actor) +
                        " FILA=" + _targetQueue.Count);
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
                    if (_kiteBoostActive) EndPunishingRush();
                    ResetKiteForNewHostOrTarget();
                    _target = nextTarget;
                    if (_target != null)
                    {
                        LogInfo("THE UNRELENTING: ALVO DEFINIDO! ANTERIOR_TEMP_ID=" +
                            (prev == null ? "NENHUM" : prev.GetHashCode().ToString()) +
                            " NOVO_TEMP_ID=" + _target.GetHashCode() +
                            " TIPO=" + GetHauntedKind(_target) +
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
                    // Nao interromper uma corrida de castigo no meio nem
                    // perder o contador antes de completar os 30s de provocacao.
                    bool waitForKite = _kiteBoostActive;
                    if (!waitForKite && _antiKiteOptions.Enabled && _kiteNearSince >= 0f &&
                        object.ReferenceEquals(_kiteHost, _host) &&
                        object.ReferenceEquals(_kiteTarget, _target) && Usable(_host) &&
                        Usable(_target) && _host.distanceToActorTile(_target) <= _antiKiteOptions.Radius)
                    {
                        float elapsedClose = Time.unscaledTime - _kiteNearSince;
                        waitForKite = elapsedClose < _antiKiteOptions.Seconds + 5f;
                    }
                    if (waitForKite)
                    {
                        _nextHostChangeTime = Time.unscaledTime + 2f;
                    }
                    else
                    {
                        Actor newHost = PickHost(alive, current, _target, _host);
                        if (newHost != null && !object.ReferenceEquals(newHost, _host))
                            SetHost(newHost, true);
                        else
                            _nextHostChangeTime = Time.unscaledTime + 10f;
                    }
                }

                // O loop global permanece a cada 2s; movimento e contato
                // sao atualizados com intervalos adaptativos independentes.
                UpdatePursuitMotion();
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
