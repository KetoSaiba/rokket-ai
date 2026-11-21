using System;
using System.Collections.Generic;
using System.Linq;
using YGOSharp.OCGWrapper;
using YGOSharp.OCGWrapper.Enums;
using WindBot;
using WindBot.Game;
using WindBot.Game.AI;

namespace WindBot.Game.AI.Decks
{
    /// <summary>
    /// KI für Ketos Deck „Rokkets“ .
    /// Eigener Zug: Der Zugplaner (RokketPlaner.cs) sucht vom echten Zustand aus die beste Zugfolge, Ziel Borrelend + Borrelshroud auf dem Feld (Keto 03.11.).
    /// Stimmt das Spiel nicht mehr mit dem Plan überein (Handfalle, gezogene Karte), wird neu geplant.
    /// Gegnerzug und Battle Phase: feste Regeln (Borrelend negiert ohne Antwort, Borrelshroud, Impermanence, Reboot, Rapid Trigger → Furious, Dillingerous).
    /// </summary>
    [Deck("RokketAI", "AI_RokketAI")]
    public class RokketAIExecutor : DefaultExecutor
    {
        // --- Main Deck ---------------------------------------------------------------------------
        private const int Detonator = 98937206, Absorouter = 67748760, Nocto = 70333910, Magnamhut = 33854624, Barrage = 53481938, Silver = 32476603,
            Magna = 26655293, Caliber = 67127799, Tracer = 68464358, Loader = 25554552, Recharger = 5969957, Auto = 80250185, Synchron = 48355999, Shel = 5087128,
            Interlock = 38129297, Droplet = 24299458, Launch = 31443476, Rapid = 67526112, Triple = 89875646, Regen = 6556178, Boot = 36668118, Imperm = 10045474, Reboot = 87607094;
        // --- Extra Deck ---------------------------------------------------------------------------
        private const int Furious = 92892239, Fatal = 84464389, Shroud = 66452432, End = 98630720, Zero = 66403530, Sword = 85289965, Liberator = 27096833,
            Code = 67288539, Tris = 3957130, Dilling = 23732205, Twin = 66015185, Breach = 90011273, Seal = 24361622, Pisty = 13143275, Striker = 73539069;

        private static readonly Dictionary<int, K> Arten = new Dictionary<int, K>
        {
            { Barrage, K.Barrage }, { Detonator, K.Detonator }, { Loader, K.Loader }, { Tracer, K.Tracer }, { Absorouter, K.Absorouter }, { Auto, K.Auto },
            { Magnamhut, K.Magnamhut }, { Magna, K.Magna }, { Nocto, K.Nocto }, { Caliber, K.Caliber }, { Recharger, K.Recharger }, { Synchron, K.Synchron },
            { Shel, K.Shel }, { Silver, K.Silver }, { Droplet, K.Droplet }, { Launch, K.Launch }, { Triple, K.Triple }, { Boot, K.Boot }, { Regen, K.Regen },
            { Interlock, K.Interlock }, { Rapid, K.Rapid }, { Imperm, K.Imperm }, { Reboot, K.Reboot }, { Striker, K.Striker }, { Pisty, K.Pisty }, { Twin, K.Twin },
            { Breach, K.Breach }, { Dilling, K.Dilling }, { Seal, K.Seal }, { Tris, K.Tris }, { Code, K.Code }, { Sword, K.Sword }, { Liberator, K.Liberator },
            { Zero, K.Zero }, { End, K.End }, { Shroud, K.Shroud }, { Fatal, K.Fatal }, { Furious, K.Furious }
        };

        /// <summary>Ketos Main Deck „Rokkets“ (Stand 2026-11-02). Bei jeder Deckänderung mitändern.</summary>
        private static readonly Dictionary<K, int> Copies = new Dictionary<K, int>
        {
            { K.Detonator, 3 }, { K.Absorouter, 1 }, { K.Nocto, 1 }, { K.Magnamhut, 1 }, { K.Barrage, 3 }, { K.Silver, 1 }, { K.Magna, 1 }, { K.Caliber, 1 },
            { K.Tracer, 3 }, { K.Loader, 3 }, { K.Recharger, 1 }, { K.Auto, 1 }, { K.Shel, 1 }, { K.Synchron, 1 }, { K.Interlock, 1 }, { K.Droplet, 3 },
            { K.Launch, 3 }, { K.Rapid, 1 }, { K.Triple, 3 }, { K.Regen, 1 }, { K.Boot, 2 }, { K.Imperm, 3 }, { K.Reboot, 1 }
        };

        /// <summary>Gegnerische Negierer und Bosse, die zuerst weg müssen (wie bei Unchained).</summary>
        private static readonly int[] GegnerStoerer = { 69248256, 9464441, 84815190, 4280258, 86066372, 29301450 };

        // --- Plan ----------------------------------------------------------------------------------
        private List<Schritt> _plan = new List<Schritt>();
        private List<Planer.Z> _planZ = new List<Planer.Z>();
        private int _pos;
        private readonly HashSet<int> _gruppeAktiv = new HashSet<int>();
        private readonly List<Wahl> _queue = new List<Wahl>();
        private bool _neuPlanen = true;
        private int _planZahl;
        private readonly HashSet<string> _verboten = new HashSet<string>();
        private List<GegnerKarte> _gegner = new List<GegnerKarte>();
        private List<ClientCard> _gegnerRef = new List<ClientCard>();
        private bool _imIdle;
        private IList<ClientCard> _angeboten;
        private ClientCard _ziel, _ziel2;
        private sbyte _tripleOption = -1, _zoneWunsch = -1, _bootOption = -1;

        // --- Merkliste des Zuges --------------------------------------------------------------------
        private readonly HashSet<Planer.E> _used = new HashSet<Planer.E>();
        private readonly List<ClientCard> _zerstoert = new List<ClientCard>();
        private readonly List<K> _matErwartet = new List<K>();
        private readonly HashSet<ClientCard> _ende = new HashSet<ClientCard>();    // Quick Launch, Synchron: gehen in der End Phase
        private readonly HashSet<ClientCard> _stumm = new HashSet<ClientCard>();   // von Furious belebt: keine Effekte diesen Zug
        private bool _warteEnde, _warteStumm, _bannDill, _nsUsed, _main2Geplant, _restGesetzt, _libOhneProc;
        private int _aktivId;

        public RokketAIExecutor(GameAI ai, Duel duel)
            : base(ai, duel)
        {
            base.AddExecutor(ExecutorType.GoToEndPhase, IdleHook);

            // Als Zweiter zuerst Negierer ausschalten (Ketos Duell 02.11. 23:15: Impermanence von der Hand, dann die Combo)
            AddExecutor(ExecutorType.Activate, Imperm, VorabImperm);
            AddExecutor(ExecutorType.Activate, Droplet, VorabDroplet);

            // Zug-Plan
            AddExecutor(ExecutorType.Activate, PlanAktivieren);
            AddExecutor(ExecutorType.Summon, PlanNormal);
            AddExecutor(ExecutorType.SpSummon, PlanLink);

            // Störungen und Antworten
            AddExecutor(ExecutorType.Activate, End, EndEffekt);
            AddExecutor(ExecutorType.Activate, Shroud, ShroudEffekt);
            AddExecutor(ExecutorType.Activate, Reboot, RebootEffekt);
            AddExecutor(ExecutorType.Activate, Imperm, ImpermEffekt);
            AddExecutor(ExecutorType.Activate, Droplet, DropletEffekt);
            AddExecutor(ExecutorType.Activate, Rapid, RapidEffekt);
            AddExecutor(ExecutorType.Activate, Furious, FuriousEffekt);
            AddExecutor(ExecutorType.Activate, Dilling, DillingEffekt);
            AddExecutor(ExecutorType.Activate, Breach, BreachEffekt);
            AddExecutor(ExecutorType.Activate, Tracer, TracerEffekt);
            AddExecutor(ExecutorType.Activate, Liberator, LiberatorEffekt);
            AddExecutor(ExecutorType.Activate, Sword, SwordEffekt);
            AddExecutor(ExecutorType.Activate, Triple, TripleEffekt);
            AddExecutor(ExecutorType.Activate, RegelAusloeser);

            // Am Ende des Zuges
            AddExecutor(ExecutorType.SpellSet, RestSetzen);
            AddExecutor(ExecutorType.Repos, MonsterRepos);
        }

        // =====================================================================================
        // Absicherung und Protokoll (WindBot\RokketAI.log)
        // =====================================================================================

        private static readonly string LogPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "RokketAI.log");

        private void Log(string text)
        {
            try
            {
                var info = new System.IO.FileInfo(LogPath);
                if (info.Exists && info.Length > 2_000_000) info.Delete();
                System.IO.File.AppendAllText(LogPath, $"{DateTime.Now:HH:mm:ss} Z{Duel.Turn} {Duel.Phase} {text}{Environment.NewLine}");
            }
            catch (Exception) { }
        }

        private static string Name(ClientCard c) => c == null ? "-" : (c.Name ?? c.Id.ToString());

        private Func<bool> Safe(ExecutorType type, Func<bool> func)
        {
            if (func == null) return null;
            return () =>
            {
                try
                {
                    bool result = func();
                    if (result)
                    {
                        Log($"{type} {Name(Card)} @{Card?.Location}");
                        if (type == ExecutorType.Activate && Card != null)
                        {
                            MerkeEffekt(Card);
                            _aktivId = Card.Id;
                        }
                        if (type == ExecutorType.Summon || type == ExecutorType.MonsterSet) _nsUsed = true;
                        if (type == ExecutorType.SpSummon && Card != null) MerkeProc(Card);
                        _imIdle = false;
                    }
                    return result;
                }
                catch (Exception e)
                {
                    Log($"FEHLER {type} {Name(Card)}: {e.Message} {e.StackTrace?.Split('\n').FirstOrDefault()}");
                    return false;
                }
            };
        }

        private new void AddExecutor(ExecutorType type, int cardId, Func<bool> func) => base.AddExecutor(type, cardId, Safe(type, func));
        private new void AddExecutor(ExecutorType type, Func<bool> func) => base.AddExecutor(type, Safe(type, func));

        // =====================================================================================
        // Hilfsfunktionen
        // =====================================================================================

        private static bool Is(ClientCard c, int id)
        {
            if (c == null || c.Id == 0) return false;
            if (c.Id == id) return true;
            int alias = c.Data != null ? c.Data.Alias : 0;
            return alias != 0 && alias == id;
        }

        private static K Art(ClientCard c)
        {
            if (c == null) return K.Anderes;
            if (Arten.TryGetValue(c.Id, out K k)) return k;
            if (c.Data != null && c.Data.Alias != 0 && Arten.TryGetValue(c.Data.Alias, out k)) return k;
            return K.Anderes;
        }

        private static O OrtVon(ClientCard c)
        {
            switch (c.Location)
            {
                case CardLocation.Hand: return O.Hand;
                case CardLocation.Deck: return O.Deck;
                case CardLocation.Grave: return O.Grab;
                case CardLocation.MonsterZone: return O.Monster;
                case CardLocation.SpellZone: return c.Sequence == 5 ? O.FeldZauber : O.Zone;
                case CardLocation.Removed: return O.Verbannt;
                default: return O.Extra;
            }
        }

        private static bool IstRokketKarte(ClientCard c)
        {
            K k = Art(c);
            return k <= K.Silver && k != K.Absorouter && k != K.Magnamhut && k != K.Nocto;
        }
        private static bool IstSchwimmer(K k) => k == K.Silver || k == K.Magna || k == K.Shel || k == K.Auto;

        private bool MyMain => Duel.Player == 0 && (Duel.Phase == DuelPhase.Main1 || Duel.Phase == DuelPhase.Main2);
        private bool GegnerMain => Duel.Player == 1 && (Duel.Phase == DuelPhase.Main1 || Duel.Phase == DuelPhase.Main2);
        private bool Kampf => Duel.Phase == DuelPhase.BattleStart || Duel.Phase == DuelPhase.Battle || Duel.Phase == DuelPhase.BattleStep || Duel.Phase == DuelPhase.Damage || Duel.Phase == DuelPhase.DamageCal;
        private static bool Unbeeinflussbar(ClientCard c) => c != null && (c.Id == 54701958 || ZerstoerSchutz.Unberuehrt(c, ZerstoerSchutz.Unbekannt));   // 08.11.: alle Immunitäten aus den Kartentexten

        private void MerkeProc(ClientCard c)
        {
            switch (Art(c))
            {
                case K.Absorouter: _used.Add(Planer.E.AbsProc); break;
                case K.Detonator: _used.Add(Planer.E.DetProc); break;
                case K.Caliber: _used.Add(Planer.E.CaliberProc); break;
                case K.Liberator: _used.Add(Planer.E.LibSS); break;
                case K.Pisty: _used.Add(Planer.E.PistyProc); break;
            }
        }

        /// <summary>Welcher Einmal-Effekt läuft: Kartenart + Ort der Aktivierung.</summary>
        private void MerkeEffekt(ClientCard c)
        {
            O o = OrtVon(c);
            bool grab = o == O.Grab, feld = o == O.Monster;
            switch (Art(c))
            {
                case K.Barrage: _used.Add(feld ? Planer.E.BarrageZ : Planer.E.BarrageHand); break;
                case K.Detonator: if (feld) _used.Add(Planer.E.DetEff); break;
                case K.Loader: if (feld) _used.Add(Planer.E.LoaderS); break;
                case K.Tracer: if (feld) _used.Add(Planer.E.TracerEff); break;
                case K.Absorouter: if (grab) _used.Add(Planer.E.AbsS); break;
                case K.Magnamhut: if (o == O.Hand) _used.Add(Planer.E.MagnamHand); break;
                case K.Magna: if (feld) _used.Add(Planer.E.MagnaZ); break;
                case K.Shel: if (feld) _used.Add(Planer.E.ShelZ); break;
                case K.Silver: if (feld) _used.Add(Planer.E.SilverZ); break;
                case K.Auto: if (feld) _used.Add(Planer.E.AutoZ); break;
                case K.Nocto: if (o == O.Hand) _used.Add(Planer.E.NoctoH); else if (grab && ActivateDescription != Util.GetStringId(Nocto, 0)) _used.Add(Planer.E.NoctoD); break;
                case K.Caliber: if (feld) _used.Add(Planer.E.CaliberTrib); break;
                case K.Synchron: if (feld) { _used.Add(Planer.E.SynchR); _warteEnde = true; } break;
                case K.Launch: _warteEnde = true; break;
                case K.Boot: if (ActivateDescription == Util.GetStringId(Boot, 0) || ActivateDescription == Util.GetStringId(Boot, 1)) _used.Add(Planer.E.BootEff); break;
                case K.Triple: _used.Add(_tripleOption == 1 ? Planer.E.TripleB : Planer.E.TripleA); break;
                case K.Regen: _used.Add(grab ? Planer.E.RegenD : Planer.E.RegenAkt); break;
                case K.Interlock: _used.Add(Planer.E.Interlock); break;
                case K.Rapid: _used.Add(Planer.E.RapidAkt); break;
                case K.Striker: _used.Add(ActivateDescription == Util.GetStringId(Striker, 0) ? Planer.E.StrikerS : Planer.E.StrikerEff); break;
                case K.Pisty: _used.Add(Planer.E.PistyEff); break;
                case K.Breach: _used.Add(ActivateDescription == Util.GetStringId(Breach, 0) ? Planer.E.BreachS : Planer.E.BreachEff); break;
                case K.Dilling: if (grab) { _used.Add(Planer.E.DillR); _bannDill = true; } break;
                case K.Tris: _used.Add(ActivateDescription == Util.GetStringId(Tris, 0) ? Planer.E.TrisS : Planer.E.TrisEff); break;
                case K.Fatal: if (grab) _used.Add(Planer.E.FatalP); else _used.Add(ActivateDescription == Util.GetStringId(Fatal, 0) ? Planer.E.FatalS : Planer.E.FatalEquip); break;
                case K.Furious: if (grab) { _used.Add(Planer.E.FuriousGrab); _warteStumm = true; } else _used.Add(Planer.E.FuriousEff); break;
                case K.End: _used.Add(Planer.E.EndEff); break;
                case K.Liberator: if (grab) _used.Add(Planer.E.LibSS); break;
                case K.Twin: _used.Add(Planer.E.TwinR); break;
            }
        }

        private bool Frei(Planer.E e) => !_used.Contains(e);

        public override bool OnSelectHand()
        {
            Log("=== Neues Duell: ROKKET AI ===");
            _libOhneProc = false;
            return true;   // als Erster baut das Deck Borrelend + Borrelshroud
        }

        public override void OnNewTurn()
        {
            ZerstoerSchutz.NeuerZug(Duel.Player); ZerstoerSchutz.Umhuellen(this, AI, () => Enemy.GetMonsters(), () => Card);
            _nsUsed = false;
            _used.Clear();
            _plan.Clear(); _planZ.Clear(); _pos = 0;
            _gruppeAktiv.Clear();
            _queue.Clear();
            _neuPlanen = true;
            _main2Geplant = false;
            _restGesetzt = false;
            _planZahl = 0;
            _verboten.Clear();
            _zerstoert.Clear();
            _matErwartet.Clear();
            _ende.Clear();
            _stumm.Clear();
            _warteEnde = false;
            _warteStumm = false;
            _imIdle = false;
            _ziel = null; _ziel2 = null;
            _tripleOption = -1; _zoneWunsch = -1;
            base.OnNewTurn();
        }

        public override void OnChaining(int player, ClientCard card)
        {
            base.OnChaining(player, card);
            if (player == 1) Log($"Gegner aktiviert {Name(card)} @{card?.Location}");
        }

        public override void OnChainEnd()
        {
            ZerstoerSchutz.KettenEnde();
            _aktivId = 0;
            base.OnChainEnd();
        }

        private HashSet<ClientCard> _feldLetzt = new HashSet<ClientCard>();

        /// <summary>Vergleicht das eigene Feld mit der letzten Abfrage: was vom Feld in den Friedhof ging, gilt als zerstört, außer es war Material der laufenden Beschwörung.
        /// Merkt, welches neue Monster von Quick Launch oder Synchron kam. WindBots OnMove fehlt in dieser Bibliothek.</summary>
        private void FeldVergleich()
        {
            var jetzt = new HashSet<ClientCard>(Bot.GetMonsters().Concat(Bot.GetSpells()).Where(c => c != null));
            foreach (var c in _feldLetzt)
            {
                if (jetzt.Contains(c)) continue;
                if (c.Location == CardLocation.Grave && c.Controller == 0)
                {
                    K k = Art(c);
                    if (!_matErwartet.Remove(k)) _zerstoert.Add(c);
                }
                _ende.Remove(c);
                if (Is(c, Dilling)) _bannDill = false;
            }
            if (_warteEnde)
            {
                var neu = jetzt.FirstOrDefault(c => !_feldLetzt.Contains(c) && c.Location == CardLocation.MonsterZone);
                if (neu != null) { _ende.Add(neu); _warteEnde = false; }
            }
            if (_warteStumm)
            {
                var neu = jetzt.FirstOrDefault(c => !_feldLetzt.Contains(c) && c.Location == CardLocation.MonsterZone);
                if (neu != null) { _stumm.Add(neu); _warteStumm = false; }
            }
            _feldLetzt = jetzt;
        }

        // =====================================================================================
        // Echter Zustand → Planer
        // =====================================================================================

        private static int GegnerWert(ClientCard c)
        {
            if (c == null) return 0;
            if (!c.HasType(CardType.Monster) && c.Location != CardLocation.MonsterZone) return c.IsFacedown() ? 16 : 12;
            if (c.IsFacedown()) return 16;
            if (!c.IsExtraCard() && c.Level < 5 && c.Attack < 2000 && !GegnerStoerer.Contains(c.Id)) return 8;
            int w = 18;
            if (c.IsExtraCard()) w += c.Attack >= 1800 ? 16 : 4;
            if (c.Level >= 8) w += 6;
            if (c.Attack >= 2500) w += 8;
            if (GegnerStoerer.Contains(c.Id) || ZerstoerSchutz.Negierer(c)) w += 30;
            if (Unbeeinflussbar(c)) w = 5;
            return w;
        }

        private Planer NeuerPlaner(out Planer.Z start)
        {
            _gegner = new List<GegnerKarte>();
            _gegnerRef = new List<ClientCard>();
            foreach (var m in Enemy.GetMonsters())
            {
                if (m == null) continue;
                _gegner.Add(new GegnerKarte
                {
                    Id = m.Id, Wert = GegnerWert(m), Atk = m.Attack, Offen = m.IsFaceup(), Immun = Unbeeinflussbar(m) || ZerstoerSchutz.KeinMonsterZiel(m) || !ZerstoerSchutz.Wirksam(m, ZerstoerSchutz.AlsMonster),
                    Unzerst = ZerstoerSchutz.Meiden(m), Monster = true, Verteidigung = m.IsDefense(), Negiert = m.IsDisabled()
                });
                _gegnerRef.Add(m);
            }
            foreach (var s in Enemy.GetSpells())
            {
                if (s == null) continue;
                _gegner.Add(new GegnerKarte { Id = s.Id, Wert = GegnerWert(s), Offen = s.IsFaceup(), Unzerst = ZerstoerSchutz.Meiden(s), Monster = false });
                _gegnerRef.Add(s);
            }
            // Gegnerisches Monster in einer Extra-Monsterzone sperrt die gegenüberliegende eigene
            int sperre = 0;
            if (Enemy.MonsterZone[5] != null) sperre |= 1 << 6;
            if (Enemy.MonsterZone[6] != null) sperre |= 1 << 5;
            var pl = new Planer(_gegner, Duel.Turn == 1, Enemy.LifePoints, _verboten, Duel.Phase == DuelPhase.Main2, sperre);
            var z = Planer.Leer();
            var gesehen = new Dictionary<K, int>();
            void Zaehle(K k) { gesehen.TryGetValue(k, out int n); gesehen[k] = n + 1; }
            var hand = new Dictionary<K, int>();
            foreach (var c in Bot.Hand)
            {
                K k = Art(c);
                hand.TryGetValue(k, out int n); hand[k] = n + 1;
                Zaehle(k);
            }
            foreach (var kv in hand) Planer.SetHand(z, kv.Key, kv.Value);
            foreach (var c in Bot.Graveyard)
            {
                K k = Art(c);
                Planer.AddGrab(z, k, _zerstoert.Contains(c));
                Zaehle(k);
            }
            foreach (var c in Bot.Banished) { K k = Art(c); Planer.AddVerbannt(z, k); Zaehle(k); }
            foreach (var m in Bot.GetMonsters())
            {
                K k = Art(m);
                byte f = 0;
                if (m.IsDisabled() || _stumm.Contains(m)) f |= Planer.FNegiert;
                if (_ende.Contains(m)) f |= (byte)(Planer.FEnde | Planer.FKeinAngriff);
                if (Is(m, Dilling) && _bannDill) f |= Planer.FBann;
                Planer.AddMonster(z, k, f, m.Sequence);
                Zaehle(k);
            }
            foreach (var s in Bot.GetSpells())
            {
                K k = Art(s);
                Zaehle(k);
                if (s.Sequence == 5) { if (k == K.Boot) Planer.SetFeldzauber(z); continue; }
                // Verdeckte Karten stehen im Plan nur als belegte Zone
                Planer.AddZone(z, s.IsFacedown() ? K.Anderes : k, s.EquipTarget != null ? s.EquipTarget.Sequence : -1);
            }
            foreach (var kv in Copies)
            {
                gesehen.TryGetValue(kv.Key, out int n);
                Planer.SetDeck(z, kv.Key, kv.Value - n);
            }
            foreach (K link in new[] { K.Striker, K.Pisty, K.Twin, K.Breach, K.Dilling, K.Seal, K.Tris, K.Code, K.Sword, K.Liberator, K.Zero, K.End, K.Shroud, K.Fatal, K.Furious })
                Planer.SetDeck(z, link, Bot.ExtraDeck.Count(c => Art(c) == link));
            // Von Borrelshroud gerufen: ohne echte Link-Beschwörung lässt sich Liberator nicht beleben
            if (_libOhneProc && Bot.Graveyard.Any(c => Is(c, Liberator))) Planer.Benutzt(z, Planer.E.LibSS);
            if (_nsUsed) Planer.NormalVerbraucht(z);
            foreach (var e in _used) if (e != Planer.E.Anzahl) Planer.Benutzt(z, e);
            // Twin Triangle kostet 500 LP: bei 1000 oder weniger nicht mehr (Ketos Cubic-Duell 04.11. 17:48, von 1000 auf 500, danach reichte der Brand)
            if (Bot.LifePoints <= 1000) Planer.Benutzt(z, Planer.E.TwinR);
            pl.SetGegner(z);
            start = z;
            return pl;
        }

        private void Planen(string grund)
        {
            FeldVergleich();
            _planZahl++;
            var uhr = System.Diagnostics.Stopwatch.StartNew();
            var pl = NeuerPlaner(out var start);
            _planZ = pl.Suche(start, _planZahl <= 1 ? 8100 : 2700, _planZahl <= 1 ? 3000 : 1500, out int wert);
            _plan = _planZ.Select(z => z.Schritt).ToList();
            _pos = 0;
            _gruppeAktiv.Clear();
            _queue.Clear();
            _neuPlanen = false;
            Log($"PLAN ({grund}) Wert {wert}, {pl.Knoten} Knoten, {uhr.ElapsedMilliseconds} ms, Start {Planer.Signatur(start)}");
            for (int i = 0; i < _plan.Count; i++) Log($"  {i + 1}. {_plan[i]}");
        }

        // =====================================================================================
        // Hauptphase: Plan prüfen und abarbeiten
        // =====================================================================================

        private static bool IstAusloeser(Schritt s) => s.Art == Decks.Art.Ausloeser || s.Art == Decks.Art.Auslassen;

        /// <summary>Läuft bei jeder Abfrage in der Hauptphase als Erstes, beantwortet nie (false).</summary>
        private bool IdleHook()
        {
            try
            {
                _imIdle = true;
                _angeboten = null;
                FeldVergleich();
                _queue.Clear();
                _matErwartet.Clear();
                _gruppeAktiv.Clear();
                _zoneWunsch = -1;
                if (!MyMain) return false;
                while (_pos < _plan.Count && IstAusloeser(_plan[_pos])) _pos++;
                bool neu = _neuPlanen;
                if (!neu && _plan.Count > 0 && !_restGesetzt)
                {
                    NeuerPlaner(out var ist);
                    var soll = _pos > 0 ? _planZ[_pos - 1] : null;
                    if (soll != null && Planer.Signatur(soll) != Planer.Signatur(ist))
                    {
                        Log($"Plan passt nicht mehr: soll {Planer.Signatur(soll)} | ist {Planer.Signatur(ist)}");
                        neu = true;
                    }
                }
                if (!neu && _plan.Count == 0 && _planZahl == 0) neu = true;
                if (!neu && _pos >= _plan.Count && _planZahl < 12)
                {
                    if (Duel.Phase == DuelPhase.Main2 && !_main2Geplant) { _main2Geplant = true; neu = true; }
                }
                if (neu && _planZahl < 12) Planen(_neuPlanen ? "neu" : "Abweichung");
                for (int v = 0; v < 6 && _pos < _plan.Count; v++)
                {
                    var s = _plan[_pos];
                    if (IstAusloeser(s) || Verfuegbar(s)) break;
                    Log($"Schritt nicht verfügbar: {s}");
                    _verboten.Add(s.Art + " " + s.Karte + " " + s.Ort);
                    if (_planZahl >= 12) { _plan.Clear(); _planZ.Clear(); _pos = 0; break; }
                    Planen("nicht verfügbar");
                }
            }
            catch (Exception e) { Log($"FEHLER Hook: {e.Message} {e.StackTrace?.Split('\n').FirstOrDefault()}"); }
            return false;
        }

        /// <summary>Zeigt das Spiel den zweiten Boot-Effekt als eigene Aktivierung? Einmal gesehen, gilt das für das ganze Duell.</summary>
        private bool BootZweiEffekteBekannt;
        private bool BootZweiEffekte()
        {
            if (Duel.MainPhase != null && Duel.MainPhase.ActivableDescs.Contains(Util.GetStringId(Boot, 1))) BootZweiEffekteBekannt = true;
            return BootZweiEffekteBekannt;
        }

        private bool Verfuegbar(Schritt s)
        {
            var main = Duel.MainPhase;
            if (main == null) return true;
            switch (s.Art)
            {
                case Decks.Art.Normal: return main.SummonableCards.Any(c => Art(c) == s.Karte);
                case Decks.Art.Proc:
                case Decks.Art.Link: return main.SpecialSummonableCards.Any(c => Art(c) == s.Karte);
                case Decks.Art.Aktivieren:
                    if (s.Karte == K.Boot && s.Ort == O.FeldZauber && s.Option == 1 && !main.ActivableDescs.Contains(Util.GetStringId(Boot, 1)) && main.ActivableDescs.Contains(Util.GetStringId(Boot, 0)) && BootZweiEffekteBekannt) return false;
                    return main.ActivableCards.Any(c => Art(c) == s.Karte && OrtVon(c) == s.Ort);
            }
            return true;
        }

        private void Queue(Schritt s, bool vorne)
        {
            if (vorne) _queue.InsertRange(0, s.Wahlen);
            else _queue.AddRange(s.Wahlen);
            if (s.Ziehen) _neuPlanen = true;
            if (s.Zone >= 0) _zoneWunsch = s.Zone;
            if (s.Karte == K.Triple) _tripleOption = s.Option;
            if (s.Karte == K.Boot && s.Option >= 0) _bootOption = s.Option;
        }

        private bool PlanNormal()
        {
            if (!_imIdle || _pos >= _plan.Count) return false;
            var s = _plan[_pos];
            if (s.Art != Decks.Art.Normal || Art(Card) != s.Karte) return false;
            _pos++;
            return true;
        }

        private bool PlanLink()
        {
            if (!_imIdle || _pos >= _plan.Count) return false;
            var s = _plan[_pos];
            if ((s.Art != Decks.Art.Link && s.Art != Decks.Art.Proc) || Art(Card) != s.Karte) return false;
            if (s.Art == Decks.Art.Proc && Card.Location != CardLocation.Hand) return false;
            _pos++;
            Queue(s, false);
            _matErwartet.AddRange(s.Wahlen.Select(w => w.Karte));
            return true;
        }

        private bool PlanAktivieren()
        {
            if (Card == null) return false;
            if (_imIdle)
            {
                if (_pos >= _plan.Count) return false;
                var s = _plan[_pos];
                if (s.Art != Decks.Art.Aktivieren || Art(Card) != s.Karte || OrtVon(Card) != s.Ort) return false;
                // Boot Sector: Die Skript-Fassungen haben entweder zwei Effekte oder einen mit Auswahl (OnSelectOption). Bei zweien den richtigen nehmen.
                if (s.Karte == K.Boot && s.Ort == O.FeldZauber && BootZweiEffekte() && ActivateDescription != Util.GetStringId(Boot, s.Option)) return false;
                _pos++;
                Queue(s, false);
                return true;
            }
            // Auslöser: die laufende Gruppe des Plans
            if (!MyMain) return false;
            var gruppe = LaufendeGruppe();
            if (gruppe.Count == 0) return false;
            K k = Art(Card); O o = OrtVon(Card);
            int hier = gruppe.FindIndex(i => _plan[i].Karte == k && _plan[i].Ort == o);
            if (hier < 0 || _plan[gruppe[hier]].Art == Decks.Art.Auslassen) return false;
            for (int j = gruppe.Count - 1; j >= 0; j--)
            {
                int i = gruppe[j];
                var s = _plan[i];
                if (s.Art == Decks.Art.Auslassen || _gruppeAktiv.Contains(i)) continue;
                bool angeboten = _angeboten == null || !_angeboten.Contains(Card)
                    ? s.Karte == k && s.Ort == o
                    : _angeboten.Any(c => Art(c) == s.Karte && OrtVon(c) == s.Ort);
                if (!angeboten) continue;
                if (s.Karte != k || s.Ort != o) return false;
                _gruppeAktiv.Add(i);
                Queue(s, true);
                return true;
            }
            return false;
        }

        private List<int> LaufendeGruppe()
        {
            while (true)
            {
                var liste = new List<int>();
                if (_pos >= _plan.Count || !IstAusloeser(_plan[_pos])) return liste;
                int g = _plan[_pos].Gruppe;
                for (int i = _pos; i < _plan.Count && IstAusloeser(_plan[i]) && _plan[i].Gruppe == g; i++) liste.Add(i);
                if (liste.All(i => _plan[i].Art == Decks.Art.Auslassen || _gruppeAktiv.Contains(i)) && liste.Any(i => _gruppeAktiv.Contains(i)))
                {
                    _pos = liste.Last() + 1;
                    _gruppeAktiv.Clear();
                    continue;
                }
                return liste;
            }
        }

        private bool PlanLehntAb()
        {
            if (!MyMain) return false;
            var gruppe = LaufendeGruppe();
            K k = Art(Card); O o = OrtVon(Card);
            return gruppe.Any(i => _plan[i].Art == Decks.Art.Auslassen && _plan[i].Karte == k && _plan[i].Ort == o);
        }

        public override void OnSelectChain(IList<ClientCard> cards)
        {
            _imIdle = false;
            _angeboten = cards;
            FeldVergleich();
            base.OnSelectChain(cards);
        }

        // =====================================================================================
        // Regeln außerhalb des Plans
        // =====================================================================================

        private ClientCard GegnerKette()
        {
            if (Duel.LastChainPlayer != 1) return null;
            var last = Util.GetLastChainCard();
            return last != null && last.Controller == 1 ? last : null;
        }

        /// <summary>Borrelend negiert ohne Antwort und holt einen Rokket: als Antwort auf den ersten Monstereffekt des Gegners, sonst vor der End Phase als Körper für den eigenen Zug.</summary>
        private bool EndEffekt()
        {
            if (Card.IsDisabled() || !Bot.Graveyard.Any(IstRokketKarte) || Bot.GetMonsterCount() >= 5 && !Bot.MonsterZone.Take(5).Any(m => m == null)) return false;
            var last = GegnerKette();
            ClientCard ziel = null;
            if (last != null && last.Location == CardLocation.MonsterZone && last.IsFaceup() && !last.IsDisabled() && !Unbeeinflussbar(last) && ZerstoerSchutz.Wirksam(last, ZerstoerSchutz.AlsMonster, true, Card) && last.HasType(CardType.Effect)) ziel = last;
            else if (Duel.Player == 1 && Duel.Phase == DuelPhase.BattleStart || Duel.Player == 0 && _imIdle && _pos >= _plan.Count && Duel.Phase == DuelPhase.Main1)
                ziel = Enemy.GetMonsters().Where(m => m != null && m.IsFaceup() && !m.IsDisabled() && !Unbeeinflussbar(m) && ZerstoerSchutz.Wirksam(m, ZerstoerSchutz.AlsMonster, true, Card) && m.HasType(CardType.Effect) && GegnerWert(m) >= 18).OrderByDescending(GegnerWert).FirstOrDefault();
            else if (Duel.Player == 1 && Duel.Phase == DuelPhase.End)
                ziel = Enemy.GetMonsters().Where(m => m != null && m.IsFaceup() && !m.IsDisabled() && !Unbeeinflussbar(m) && ZerstoerSchutz.Wirksam(m, ZerstoerSchutz.AlsMonster, true, Card) && m.HasType(CardType.Effect)).OrderByDescending(GegnerWert).FirstOrDefault()
                    ?? Bot.GetMonsters().Where(m => m != null && m.IsFaceup() && !m.IsDisabled() && m.HasType(CardType.Effect) && !Is(m, End) && !Is(m, Shroud)).OrderBy(EigenWert).FirstOrDefault();
            if (ziel == null) return false;
            _ziel = ziel;
            Log($"Borrelend negiert {Name(ziel)}");
            return true;
        }

        /// <summary>Borrelshroud (Main Phase, ohne Antwort): 1 eigenes Rokket-Monster zerstören, 1 offene Gegnerkarte negieren. Schwimmer als Futter.</summary>
        private bool ShroudEffekt()
        {
            if (Card.IsDisabled()) return false;
            if (Duel.Phase == DuelPhase.BattleStart)
            {
                // Beschwörung zu Beginn der Battle Phase
                // Auch in der Battle Phase des Gegners: Liberator stiehlt dann einen Angreifer (Eternal Duelist, Guide 03.11.)
#if !OHNE_SHROUDGEGNER
                if (Duel.Player == 1 && !Enemy.GetMonsters().Any(m => m != null && m.IsAttack() && !Unbeeinflussbar(m))) return false;
                if (!Frei(Planer.E.LibSS) || !Bot.ExtraDeck.Any(c => Is(c, Liberator))) return false;
#else
                if (Duel.Player != 0 || !Frei(Planer.E.LibSS) || !Bot.ExtraDeck.Any(c => Is(c, Liberator))) return false;
#endif
                _libOhneProc = true;
                _used.Add(Planer.E.LibSS);
                return true;
            }
            if (Duel.Phase != DuelPhase.Main1 && Duel.Phase != DuelPhase.Main2) return false;
            var futter = Bot.GetMonsters().Where(m => m != null && m.IsFaceup() && IstRokketKarte(m))
                .OrderBy(m => Is(m, Magna) && !m.IsDisabled() && Frei(Planer.E.MagnaZ) && Enemy.GetMonsterCount() >= 2 ? -1 : EigenWert(m)).FirstOrDefault();
            if (futter == null) return false;
            var last = GegnerKette();
            ClientCard ziel = null;
            if (last != null && (last.Location == CardLocation.MonsterZone || last.Location == CardLocation.SpellZone) && last.IsFaceup() && !last.IsDisabled() && !Unbeeinflussbar(last)) ziel = last;
            if (ziel == null && Duel.Player == 0 && _imIdle && _pos >= _plan.Count)
                ziel = Enemy.GetMonsters().Where(m => m != null && m.IsFaceup() && !m.IsDisabled() && !Unbeeinflussbar(m) && ZerstoerSchutz.Wirksam(m, ZerstoerSchutz.AlsMonster, true, Card) && m.HasType(CardType.Effect) && GegnerWert(m) >= 34).OrderByDescending(GegnerWert).FirstOrDefault();
            if (ziel == null) return false;
            _ziel = futter; _ziel2 = ziel;
            Log($"Borrelshroud negiert {Name(ziel)}, Futter {Name(futter)}");
            return true;
        }

        private ClientCard VorabZiel() => Enemy.GetMonsters()
            .Where(m => m != null && m.IsFaceup() && !m.IsDisabled() && !Unbeeinflussbar(m) && ZerstoerSchutz.Negierer(m))
            .OrderByDescending(GegnerWert).FirstOrDefault();

        private bool VorabImperm()
        {
            if (!_imIdle || !MyMain || Duel.Phase != DuelPhase.Main1 || Duel.Turn == 1 || Card.Location != CardLocation.Hand || Bot.GetFieldCount() > 0) return false;
            var z = VorabZiel();
            if (z == null) return false;
            _ziel = z;
            _neuPlanen = true;
            Log($"Impermanence von der Hand auf Negierer {Name(z)}");
            return true;
        }

        /// <summary>Droplet von der Hand vor der Combo, wenn eine entbehrliche Karte als Kosten da ist.</summary>
        private bool VorabDroplet()
        {
            if (!_imIdle || !MyMain || Duel.Phase != DuelPhase.Main1 || Duel.Turn == 1 || Card.Location != CardLocation.Hand) return false;
            if (_used.Contains(Planer.E.Anzahl)) return false;
            var z = VorabZiel();
            if (z == null || !Bot.Hand.Any(c => c != Card && AbwurfWert(Art(c)) <= 8)) return false;
            _used.Add(Planer.E.Anzahl);
            _ziel2 = z;
            _neuPlanen = true;
            Log($"Droplet von der Hand auf Negierer {Name(z)}");
            return true;
        }

        private bool RebootEffekt()
        {
            var last = GegnerKette();
            if (last == null) return false;
            if (Card.Location == CardLocation.Hand && Bot.LifePoints < 4000) return false;
            return last.HasType(CardType.Spell) || last.HasType(CardType.Trap);
        }

        private bool ImpermEffekt()
        {
            if (Card.Location == CardLocation.Hand && Bot.GetFieldCount() > 0) return false;
            if (Card.IsDisabled() && Card.Location == CardLocation.SpellZone) return false;
            var last = GegnerKette();
            if (last != null && last.Location == CardLocation.MonsterZone && last.IsFaceup() && !last.IsDisabled() && !Unbeeinflussbar(last) && GegnerWert(last) >= 18)
            {
                _ziel = last;
                return true;
            }
            if (Duel.Player == 1 && (Duel.Phase == DuelPhase.Main1 || Duel.Phase == DuelPhase.BattleStart))
            {
                var z = Enemy.GetMonsters().Where(m => m != null && m.IsFaceup() && !m.IsDisabled() && !Unbeeinflussbar(m) && GegnerWert(m) >= 34).OrderByDescending(GegnerWert).FirstOrDefault();
                if (z != null) { _ziel = z; return true; }
            }
            return false;
        }

        private bool DropletEffekt()
        {
            if (_imIdle) return false;
            var last = GegnerKette();
            if (last == null || last.Location != CardLocation.MonsterZone || !last.IsFaceup() || last.IsDisabled() || Unbeeinflussbar(last) || !last.HasType(CardType.Effect)) return false;
            if (GegnerWert(last) < 24) return false;
            if (!Bot.Hand.Any(c => c != null) && !Bot.GetMonsters().Any(m => m != null && m.IsDisabled())) return false;
            _ziel2 = last;
            return true;
        }

        private ClientCard BestesFurious() => Enemy.GetMonsters().Concat(Enemy.GetSpells()).Where(c => c != null && !ZerstoerSchutz.Verpufft(c, ZerstoerSchutz.AlsMonster)).OrderByDescending(GegnerWert).FirstOrDefault();

        /// <summary>Rapid Trigger im Gegnerzug: 2 kleine Monster werden Furious und räumen eine Karte. Duell 02.11. 23:04.</summary>
        private bool RapidEffekt()
        {
            if (_imIdle || Card.Location != CardLocation.SpellZone) return false;
            if (Duel.Player != 1 || !(Duel.Phase == DuelPhase.Main1 || Duel.Phase == DuelPhase.BattleStart || Duel.Phase == DuelPhase.Main2)) return false;
            if (!Bot.ExtraDeck.Any(c => Is(c, Furious))) return false;
            if (Bot.GetMonsters().Count(m => m != null && m.IsFaceup() && Art(m) <= K.Silver) < 2) return false;
            var z = BestesFurious();
            return z != null && GegnerWert(z) >= 18 && (GegnerKette() != null || Duel.Phase == DuelPhase.BattleStart);
        }

        private bool FuriousEffekt()
        {
            if (Card.Location == CardLocation.Grave)
            {
                if (_imIdle) return false;
                return false;
            }
            if (Card.IsDisabled() || _imIdle && _pos < _plan.Count) return false;
            var g = BestesFurious();
            if (g == null || GegnerWert(g) < 18) return false;
            var eigen = Bot.GetMonsters().Where(m => m != null && m != Card && m.IsFaceup() && !Is(m, End) && !Is(m, Shroud)).OrderBy(EigenWert).FirstOrDefault();
            if (eigen == null) return false;
            _ziel = eigen; _ziel2 = g;
            return true;
        }

        /// <summary>Dillingerous, End Phase des Gegners: Angriffsmonster zerstören und Schaden in Höhe seiner Original-ATK.</summary>
        private bool DillingEffekt()
        {
            if (Card.Location == CardLocation.Grave) return !PlanLehntAb() && Bot.MonsterZone.Take(5).Any(m => m == null);
            if (Card.IsDisabled() || Duel.Player != 1 || Duel.Phase != DuelPhase.End) return false;
            var z = Enemy.GetMonsters().Where(m => m != null && m.IsAttack() && !m.Attacked && !ZerstoerSchutz.Verpufft(m, ZerstoerSchutz.AlsMonster)).OrderByDescending(m => m.BaseAttack).FirstOrDefault();
            if (z == null) return false;
            _ziel = z;
            return true;
        }

        private ClientCard BreachZiel(params K[] arten)
            => Bot.GetMonsters().FirstOrDefault(m => m != null && m.IsFaceup() && !m.IsDisabled() && arten.Contains(Art(m)));

        /// <summary>Breachborrel außerhalb des Plans: Magnarokket schickt ein Gegnermonster weg (Duell 16:40), in der End Phase des Gegners holt ein Schwimmer zwei Körper.</summary>
        private bool BreachEffekt()
        {
            if (Card.IsDisabled() || _imIdle) return false;
            if (ActivateDescription == Util.GetStringId(Breach, 0)) return !PlanLehntAb();
            if (Duel.Player != 1 || !Bot.Deck.Any()) return false;
            ClientCard x = null;
            if (Frei(Planer.E.MagnaZ) && Enemy.GetMonsters().Any(m => m != null && !Unbeeinflussbar(m) && GegnerWert(m) >= 18)) x = BreachZiel(K.Magna);
            if (x == null && Duel.Phase == DuelPhase.End) x = BreachZiel(K.Barrage, K.Silver, K.Shel, K.Auto);
            if (x == null) return false;
            _ziel = x;
            Log($"Breachborrel zielt auf {Name(x)}");
            return true;
        }

        private bool TracerEffekt()
        {
            if (Card.IsDisabled() || _imIdle || Card.Location != CardLocation.MonsterZone || Duel.Player != 1 || Duel.Phase != DuelPhase.End) return false;
            _ziel = Card;   // End Phase des Gegners: wird selbst zerstört und holt Loader, der wieder sucht
            return true;
        }

        private bool LiberatorEffekt()
        {
            if (Card.Location == CardLocation.Grave)
            {
                if (_imIdle && _pos < _plan.Count) return false;
                if (!Frei(Planer.E.LibSS) || _libOhneProc || Bot.GetMonsters().Any(m => m != null && Is(m, Liberator))) return false;
                if (!(Duel.Player == 0 && (Duel.Phase == DuelPhase.Main1 || Duel.Phase == DuelPhase.BattleStart))) return false;
                var futter = Bot.GetMonsters().Where(m => m != null && m.IsFaceup() && !Is(m, End) && !Is(m, Shroud) && !m.IsExtraCard()).OrderBy(EigenWert).FirstOrDefault();
                if (futter == null) return false;
                _ziel = futter;
                return true;
            }
#if !OHNE_SHROUDGEGNER
            if (Card.IsDisabled() || !Kampf) return false;
#else
            if (Card.IsDisabled() || Duel.Player != 0 || !Kampf) return false;
#endif
            return Enemy.GetMonsters().Any(m => m != null && !Unbeeinflussbar(m));
        }

        private bool SwordEffekt()
        {
            if (Card.IsDisabled() || Duel.Player != 0 || Duel.Phase != DuelPhase.BattleStart) return false;
            var z = Enemy.GetMonsters().Where(m => m != null && m.IsAttack() && !Unbeeinflussbar(m)).OrderByDescending(m => m.Attack).FirstOrDefault();
            if (z == null) return false;
            _ziel = z;
            return true;
        }

        /// <summary>Triple Borrel Launch, End Phase des Gegners: 1 Drache zurück, Rokket aus dem Deck als Material für den eigenen Zug.</summary>
        private bool TripleEffekt()
        {
            if (_imIdle || Duel.Player != 1 || Duel.Phase != DuelPhase.End || Card.Location != CardLocation.SpellZone) return false;
            if (!Bot.Graveyard.Any(c => Art(c) <= K.Silver) || !Bot.MonsterZone.Take(5).Any(m => m == null)) return false;
            _tripleOption = 0;
            return true;
        }

        /// <summary>Eigene Karte als Zerstörungsziel oder Futter: niedrig = besser.</summary>
        private int EigenWert(ClientCard c)
        {
            K k = Art(c);
            if (_ende.Contains(c)) return 0;
            switch (k)
            {
                case K.End: case K.Shroud: return 1000;
                case K.Silver: return Frei(Planer.E.SilverZ) ? 2 : 3;
                case K.Shel: return 3;
                case K.Auto: return 4;
                case K.Magna: return 5;
                case K.Loader: return 6;
                case K.Absorouter: return 6;
                case K.Barrage: return 7;
                case K.Tracer: return 8;
                case K.Caliber: return 9;
                case K.Nocto: return 10;
                case K.Synchron: return 1;
                case K.Recharger: return 11;
                case K.Detonator: return 12;
                case K.Striker: case K.Pisty: return 14;
                case K.Twin: case K.Dilling: case K.Breach: return 20;
                case K.Fatal: return 24;
                case K.Tris: return 28;
                case K.Liberator: case K.Sword: case K.Furious: return 60;
                case K.Boot: return 30;
                default: return 40;
            }
        }

        /// <summary>Auslöser, die der Plan nicht vorhergesehen hat (Gegnerzug, Abweichung, End Phase): nutzen, kosten nichts.</summary>
        private bool RegelAusloeser()
        {
            if (_imIdle || Card == null) return false;
            K k = Art(Card); O o = OrtVon(Card);
            if (PlanLehntAb()) return false;
            switch (k)
            {
                case K.Loader: return o == O.Monster && !Card.IsDisabled();
                case K.Absorouter: return o == O.Grab;
                case K.Nocto: return o == O.Grab || o == O.Hand;
                case K.Regen: return o == O.Grab;
                case K.Fatal:
                    if (o == O.Grab) return ZerstoerSchutz.HatZiel(Enemy.GetMonsters().Concat(Enemy.GetSpells()), ZerstoerSchutz.AlsMonster);
                    return o == O.Monster && ActivateDescription == Util.GetStringId(Fatal, 0);
                case K.Striker: return o == O.Monster && ActivateDescription == Util.GetStringId(Striker, 0);
                case K.Tris: return o == O.Monster && ActivateDescription == Util.GetStringId(Tris, 0);
                case K.Twin: return o == O.Monster && Bot.LifePoints > 1000;
                case K.Synchron: return o == O.Monster;
                case K.Magnamhut: return o == O.Monster;
                case K.Recharger: return true;
                case K.Silver: case K.Magna: case K.Shel: case K.Auto:
                    if (o == O.Grab) return true;
                    return o == O.Monster && Reaktion();
                case K.Barrage: return o == O.Monster && Reaktion();
                default: return false;
            }
        }

        /// <summary>Link-Effekt zielt auf diese Karte: selbst zerstören und den Effekt nutzen. Gegner immer, eigener nur für Breachborrel außerhalb des Plans.</summary>
        private bool Reaktion()
        {
            var last = Util.GetLastChainCard();
            if (last == null || !(Duel.ChainTargets ?? new List<ClientCard>()).Contains(Card)) return false;
            // Borrelshroud negiert auch, wenn das Ziel schon weg ist (Skript): Magnarokket räumt dann zusätzlich ein Monster ab (Video 03.11.)
            return last.Controller == 1 || Is(last, Breach) || Is(last, Shroud);
        }

        private bool RestSetzen()
        {
            if (!MyMain || _pos < _plan.Count) return false;
            bool setzen = Is(Card, Imperm) || Is(Card, Reboot) || Is(Card, Rapid) || Is(Card, Droplet) || Is(Card, Triple);
            if (setzen && Bot.GetSpells().Count(s => s != null && s.Sequence < 5) >= 4) return false;
            if (setzen) _restGesetzt = true;
            return setzen;
        }

        private bool MonsterRepos()
        {
            if (Duel.Turn == 1) return false;
            return DefaultMonsterRepos();
        }

        // =====================================================================================
        // Fragen beantworten
        // =====================================================================================

        public override CardPosition OnSelectPosition(int cardId, IList<CardPosition> positions)
        {
            NamedCard data = NamedCard.Get(cardId);
            if (data != null && (data.Type & (int)CardType.Link) != 0) return CardPosition.FaceUpAttack;
            if (Duel.Player == 1 || Duel.Turn == 1 || Duel.Phase == DuelPhase.End) return CardPosition.FaceUpDefence;
            if (data != null && data.Attack >= 1500 || Enemy.GetMonsterCount() == 0) return CardPosition.FaceUpAttack;
            return CardPosition.FaceUpDefence;
        }

        public override bool OnSelectYesNo(long desc)
        {
            try
            {
                if (desc == Util.GetStringId(Breach, 2)) return true;    // Ziel ist weg: Rokket aus dem Deck
                if (desc == Util.GetStringId(Triple, 4)) return true;    // Rokket aus dem Deck
            }
            catch (Exception e) { Log($"FEHLER JaNein: {e.Message}"); }
            return base.OnSelectYesNo(desc);
        }

        public override int OnSelectOption(IList<long> options)
        {
            try
            {
                // Boot Sector: ein Effekt mit Auswahl, 1 = von der Hand, 2 = aus dem Friedhof. Vorher immer die erste Option (Ketos Duell 03.11. 20:25: Combo blieb stehen)
                int bh = options.IndexOf(Util.GetStringId(Boot, 1)), bg = options.IndexOf(Util.GetStringId(Boot, 2));
                if (bh >= 0 || bg >= 0)
                {
                    bool grab = _bootOption == 1;
                    _bootOption = -1;
                    if (grab && bg >= 0) return bg;
                    if (bh >= 0) return bh;
                    return bg;
                }
                // Triple Borrel: 1. Effekt holt einen Rokket, 2. Effekt holt Boot Sector zurück
                int i = options.IndexOf(Util.GetStringId(Triple, _tripleOption == 1 ? 2 : 1));
                if (i >= 0) return i;
                if (options.Contains(Util.GetStringId(Triple, 3))) return options.IndexOf(Util.GetStringId(Triple, 3));
            }
            catch (Exception e) { Log($"FEHLER Option: {e.Message}"); }
            return base.OnSelectOption(options);
        }

        public override int OnSelectPlace(long cardId, int player, CardLocation location, int available)
        {
            try
            {
                if (player == 0 && location == CardLocation.MonsterZone)
                {
                    if (_zoneWunsch >= 0 && (available & (1 << _zoneWunsch)) != 0)
                    {
                        int w = 1 << _zoneWunsch;
                        _zoneWunsch = -1;
                        return w;
                    }
                    NamedCard data = NamedCard.Get((int)cardId);
                    bool link = data != null && (data.Type & (int)CardType.Link) != 0;
                    int emz = available & ((1 << 5) | (1 << 6));
                    if (link && (available & 0x1f) == 0 && emz != 0) return (emz & (1 << 5)) != 0 ? 1 << 5 : 1 << 6;
                    int verlinkt = 0;
                    foreach (var m in Bot.GetMonsters())
                        if (m != null && Planer.IstLink(Art(m))) verlinkt |= Planer.Zeigt(Art(m), m.Sequence);
                    int zone = Planer.ZoneMain(available, verlinkt);
                    if (zone >= 0) return 1 << zone;
                    if (emz != 0) return (emz & (1 << 5)) != 0 ? 1 << 5 : 1 << 6;
                }
            }
            catch (Exception e) { Log($"FEHLER Zone: {e.Message}"); }
            return base.OnSelectPlace(cardId, player, location, available);
        }

        // =====================================================================================
        // Karten auswählen
        // =====================================================================================

        private ClientCard Treffer(Wahl w, IList<ClientCard> cards)
        {
            if (w.Gegner >= 0)
            {
                if (w.Gegner < _gegnerRef.Count && cards.Contains(_gegnerRef[w.Gegner])) return _gegnerRef[w.Gegner];
                return null;
            }
            var passend = cards.Where(c => c.Controller == 0 && Art(c) == w.Karte).ToList();
            if (passend.Count == 0) return null;
            var ort = passend.Where(c => OrtVon(c) == w.Ort).ToList();
            if (ort.Count == 0) return null;
            // Zwei gleiche Monster: das nehmen, das der Plan meint (negiert oder Quick-Launch-Monster)
            if (w.Ort == O.Monster && ort.Count > 1)
            {
                bool neg = (w.Flags & Planer.FNegiert) != 0, ende = (w.Flags & Planer.FEnde) != 0;
                var genau = ort.FirstOrDefault(c => (c.IsDisabled() || _stumm.Contains(c)) == neg && _ende.Contains(c) == ende);
                if (genau != null) return genau;
                genau = ort.FirstOrDefault(c => (c.IsDisabled() || _stumm.Contains(c)) == neg);
                if (genau != null) return genau;
            }
            return ort.FirstOrDefault();
        }
        // 08.11. (Keto: nie Effekte auf immune Monster): Aktivierung, deren Ziele alle immun waren, nicht wiederholen,
        // solange kein neues Gegnermonster dazukommt (gemeinsam\ZerstoerSchutzImmun.cs).
        public override bool OnPreActivate(ClientCard card)
        {
            if (ZerstoerSchutz.Gesperrt(card, Enemy.GetMonsters())) return false;
            return base.OnPreActivate(card);
        }


        public override IList<ClientCard> OnSelectCard(IList<ClientCard> cards, int min, int max, long hint, bool cancelable)
        {
            try
            {
                IList<ClientCard> result = null;
                try { result = Auswahl(cards, min, max, hint); }
                catch (Exception e) { Log($"FEHLER Auswahl {hint}: {e.Message} {e.StackTrace?.Split('\n').FirstOrDefault()}"); }
                result = ZerstoerSchutz.Pruefe(result, cards, min, max, hint, AI, cancelable, Card, Duel.CurrentChain.LastOrDefault(k => k != null && k.Controller == 0), GegnerWert);
                if (result != null)
                {
                    Log($"  Auswahl {hint} ({min}-{max}): {string.Join(", ", result.Select(Name))}");
                    return result;
                }
                Log($"  Auswahl {hint} ({min}-{max}) STANDARD aus: {string.Join(", ", cards.Select(c => $"{Name(c)}@{c.Location}{(c.Controller == 1 ? "/G" : "")}"))}");
                return base.OnSelectCard(cards, min, max, hint, cancelable);
            }
            finally
            {
                // Nach dem Mischen stimmen gemerkte IDs der Deck-Plätze nicht mehr (siehe SUN GOD AI).
                foreach (ClientCard c in Bot.Deck)
                    if (c != null && c.Id != 0)
                        c.SetId(0);
            }
        }

        private IList<ClientCard> Auswahl(IList<ClientCard> cards, int min, int max, long hint)
        {
            // 1. Plan
            var gewaehlt = new List<ClientCard>();
            for (int i = 0; i < _queue.Count && gewaehlt.Count < max; )
            {
                var c = Treffer(_queue[i], cards.Where(x => !gewaehlt.Contains(x)).ToList());
                if (c != null) { gewaehlt.Add(c); _queue.RemoveAt(i); }
                else i++;
            }
            if (gewaehlt.Count >= Math.Max(1, min)) return gewaehlt;
            if (gewaehlt.Count > 0) return Util.CheckSelectCount(gewaehlt, cards, min, max);
            // Freiwillige weitere Auswahl: nichts dazunehmen
            if (min == 0) return new List<ClientCard>();
            // 2. Vorwahl aus einer Regel
            if (AI.HaveSelectedCards()) return null;
            // 3. Regeln
            return RegelWahl(cards, min, max, hint);
        }

        private static int SsPrioWert(K k)
        {
            switch (k)
            {
                case K.End: return 0;
                case K.Shroud: return 1;
                case K.Liberator: return 2;
                case K.Sword: return 3;
                case K.Furious: return 4;
                case K.Tris: return 5;
                case K.Magna: return 6;
                case K.Barrage: return 7;
                case K.Loader: return 8;
                case K.Silver: return 9;
                case K.Tracer: return 10;
                case K.Shel: return 11;
                case K.Auto: return 12;
                case K.Recharger: return 13;
                case K.Detonator: return 14;
                case K.Caliber: return 15;
                case K.Fatal: return 16;
                default: return 30;
            }
        }
        private static int HandWert(K k)
        {
            switch (k)
            {
                case K.Detonator: return 0;
                case K.Barrage: return 1;
                case K.Tracer: return 2;
                case K.Loader: return 3;
                case K.Recharger: return 4;
                case K.Caliber: return 5;
                case K.Rapid: return 6;
                case K.Reboot: return 7;
                case K.Boot: return 8;
                default: return 15;
            }
        }
        private static int AbwurfWert(K k)
        {
            switch (k)
            {
                case K.Interlock: return 0;
                case K.Regen: return 1;
                case K.Boot: return 2;
                case K.Synchron: return 3;
                case K.Shel: return 4;
                case K.Auto: return 5;
                case K.Absorouter: return 6;     // sucht aus dem Friedhof
                case K.Silver: return 7;
                case K.Magna: return 8;
                case K.Loader: return 9;
                case K.Nocto: return 10;
                case K.Launch: return 11;
                case K.Triple: return 12;
                default: return 15;
            }
        }

        private IList<ClientCard> RegelWahl(IList<ClientCard> cards, int min, int max, long hint)
        {
            int n = Math.Max(1, min);
            // Vorgemerkte Ziele
            if (_ziel != null && cards.Contains(_ziel))
            {
                var z = _ziel; _ziel = null;
                return Util.CheckSelectCount(new List<ClientCard> { z }, cards, min, max);
            }
            if (_ziel2 != null && cards.Contains(_ziel2))
            {
                var z = _ziel2; _ziel2 = null;
                return Util.CheckSelectCount(new List<ClientCard> { z }, cards, min, max);
            }
            // Nur Gegnerkarten: höchster Wert
            if (cards.All(c => c.Controller == 1))
                return cards.OrderByDescending(GegnerWert).Take(n).ToList();
            // Handkarten abwerfen, Handlimit, Droplet-Kosten
            if (cards.All(c => c.Location == CardLocation.Hand) && (hint == HintMsg.Discard || hint == HintMsg.ToGrave || Duel.Phase == DuelPhase.End))
                return cards.OrderBy(c => AbwurfWert(Art(c))).Take(n).ToList();
            if (_aktivId == Droplet && cards.Any(c => c.Location == CardLocation.Hand))
                return cards.Where(c => c.Location == CardLocation.Hand).OrderBy(c => AbwurfWert(Art(c))).Take(n).ToList();
            // Aus Hand, Deck, Friedhof, Verbannung, Extra Deck
            if (cards.All(c => c.Location == CardLocation.Deck || c.Location == CardLocation.Hand || c.Location == CardLocation.Grave || c.Location == CardLocation.Removed || c.Location == CardLocation.Extra))
            {
                if (hint == HintMsg.AddToHand)
                    return cards.OrderBy(c => HandWert(Art(c))).Take(n).ToList();
                if (hint == HintMsg.ToDeck)
                    return cards.OrderByDescending(c => SsPrioWert(Art(c))).Take(n).ToList();
                return cards.OrderBy(c => SsPrioWert(Art(c))).Take(n).ToList();
            }
            // Eigene Karten: das Unwichtigste, Gegner zuerst
            var gegner = cards.Where(c => c.Controller == 1 && !Unbeeinflussbar(c)).OrderByDescending(GegnerWert).ToList();
            if (gegner.Count > 0 && hint != HintMsg.LinkMaterial) return gegner.Take(n).ToList();
            return cards.OrderBy(EigenWert).Take(n).ToList();
        }
    }
}
