using System.Collections.Generic;
using System.Linq;
using YGOSharp.OCGWrapper.Enums;
using WindBot;
using WindBot.Game;

namespace WindBot.Game.AI.Decks
{
    /// <summary>Eintrag der Immunitäts-Tabelle (immun_tabelle.py). Bits der Quelle: 1 Monster, 2 Zauber, 4 Fallen.</summary>
    internal struct Immunitaet
    {
        public readonly byte Bits, Art; public readonly int Wert;
        public Immunitaet(int bits, int art, int wert) { Bits = (byte)bits; Art = (byte)art; Wert = wert; }
    }

    /// <summary>Keto 08.11.: Jede KI prüft, ob ein Gegnermonster gegen den Effekt immun ist („unaffected by …“, „cannot be targeted by …“),
    /// und setzt nie Effekte darauf ein. Daten aus den Kartentexten, dazu Bedingungen, die die KI im Spiel auswerten kann:
    /// Zug des Gegners (God-Surpassing Servant), ATK kleiner (Divine Anima, Vagrada), Original-ATK bis N (Crimson Nova), Stufe kleiner (Apoqliphort).</summary>
    internal static partial class ZerstoerSchutz
    {
        private const byte Immer = 0, Bedingt = 1, ImGegnerzug = 2, AtkKleiner = 3, AtkSchwelle = 4, StufeKleiner = 5;

        /// <summary>Wer gerade am Zug ist (0 = diese KI), gesetzt von NeuerZug(Duel.Player). -1 = unbekannt.</summary>
        private static int _amZug = -1;

        /// <summary>Tabelle nachschlagen: erst die Hauptkennung (Alias), dann die eigene Kennung. Alternativ-Artworks wie
        /// „The Sun God of Darkness“ 101403130 (Alias 101403030) stehen oft nur unter einer der beiden (Test 08.11.: Ra wurde trotzdem gezielt).</summary>
        internal static bool Suche<T>(Dictionary<int, T> d, ClientCard c, out T wert)
        {
            if (d.TryGetValue(Kennung(c), out wert)) return true;
            return d.TryGetValue(c.Id, out wert);
        }
        internal static bool Suche(HashSet<int> d, ClientCard c) => d.Contains(Kennung(c)) || d.Contains(c.Id);

        private static int Stufe(ClientCard c) => c == null ? 0 : c.HasType(CardType.Xyz) ? c.Rank : c.Level;

        /// <summary>Kann der Effekt dieser Quelle die Karte nicht berühren? quelleKarte darf null sein, dann zählt nur quelleArt.
        /// strikt: nur sicher auswertbare Immunität; sonst auch „Bedingt“ (dann besser ein anderes Ziel nehmen).</summary>
        internal static bool Unberuehrt(ClientCard c, ClientCard quelleKarte, int quelleArt, bool strikt)
        {
            if (!Wirkt(c) || c.Location != CardLocation.MonsterZone) return false;
            int q = quelleKarte != null ? Art(quelleKarte) : quelleArt;
            // Alte Sonderregel Anima/Vagrada gilt weiter
            if (AtkImmun(c, quelleKarte)) return true;
            if (FeldImmun(c, quelleKarte, q, strikt)) return true;
            if (!Suche(ImmunDaten, c, out Immunitaet e) || !Trifft(e.Bits, q)) return false;
            switch (e.Art)
            {
                case Immer: return true;
                case ImGegnerzug:
                    if (_amZug < 0) return !strikt;
                    return _amZug != c.Controller;
                case AtkKleiner:
                    if (quelleKarte == null || q != AlsMonster) return !strikt && (q & AlsMonster) != 0;
                    return quelleKarte.Attack < c.Attack;
                case AtkSchwelle:
                    if (quelleKarte == null || q != AlsMonster) return !strikt && (q & AlsMonster) != 0;
                    {
                        int basis = quelleKarte.Data != null ? quelleKarte.Data.Attack : quelleKarte.BaseAttack;
                        return basis <= e.Wert && quelleKarte != c;
                    }
                case StufeKleiner:
                    if (quelleKarte == null || q != AlsMonster) return !strikt && (q & AlsMonster) != 0;
                    return Stufe(quelleKarte) < Stufe(c);
                default: return !strikt;   // Bedingt
            }
        }

        /// <summary>Das laufende Duell (aus Umhuellen), für Immunitäten, die eine andere Karte auf dem Feld verleiht.</summary>
        private static Duel _duel;

        private static readonly HashSet<int> ShrineIds = new HashSet<int> { 101403062, 511600282 };
        private static readonly HashSet<int> KingIds = new HashSet<int> { 101403048, 101403148 };
        private const int SalamangreatSet = 0x119, SacredBeastSet = 0x145, ParadiseId = 65861210;
        private static bool IstParadise(ClientCard c) => c != null && c.IsFaceup() && c.Id == ParadiseId;

        private static bool IstShrine(ClientCard c) => c != null && c.IsFaceup() && !c.IsDisabled() && (ShrineIds.Contains(c.Id) || c.Name == "Salamangreat Shrine");
        private static bool IstKing(ClientCard c) => c != null && c.IsFaceup() && !c.IsDisabled() && c.Location == CardLocation.MonsterZone
            && (KingIds.Contains(c.Id) || c.Name != null && c.Name.StartsWith("The King of Beasts that Rises"));
        private static bool Feuer(ClientCard c) => c.HasAttribute(CardAttribute.Fire) || c.Data != null && (c.Data.Attribute & (int)CardAttribute.Fire) != 0;

        /// <summary>10.11. (Turnier f52): Salamangreat Shrine macht „Salamangreat“-Monster unbeeinflusst von aktivierten Effekten gegnerischer FEUER-Monster,
        /// King of Beasts macht alle offenen Monster zu FEUER. Zusammen wirkt kein Monstereffekt vom Feld mehr (Raviel opferte 2 Monster, Cerberus warf ab,
        /// Phoenix zahlte LP, Borrelend negierte, alles ohne Wirkung). Die Tabelle aus den Kartentexten kennt nur Immunität der Karte selbst.</summary>
        private static bool FeldImmun(ClientCard c, ClientCard quelleKarte, int q, bool strikt)
        {
#if OHNE_FELDIMMUN
            return false;
#else
            try
            {
                if (_duel == null) return false;
                var seite = _duel.Fields[c.Controller];
                // Fallen Paradise of the Sacred Beasts: das damit beschworene „Sacred Beast“ ist unbeeinflusst von aktivierten Zaubern/Fallen des Gegners.
                // Welches Monster so kam, sieht die KI nicht → nur „meiden“ (nicht strikt), wenn Fallen Paradise offen liegt oder im Friedhof ist.
                if ((q == AlsZauber || q == AlsFalle) && !strikt && c.IsSpecialSummoned && c.HasSetcode(SacredBeastSet)
                    && (seite.GetSpells().Any(IstParadise) || seite.Graveyard.Any(g => g != null && g.Id == ParadiseId))) return true;
                if (q != AlsMonster || !c.HasSetcode(SalamangreatSet)) return false;
                if (!IstShrine(seite.GetFieldSpellCard()) && !seite.GetSpells().Any(IstShrine)) return false;
                bool king = _duel.Fields[0].GetMonsters().Concat(_duel.Fields[1].GetMonsters()).Any(IstKing);
                if (quelleKarte == null) return king || !strikt;
                if (quelleKarte.Location == CardLocation.MonsterZone && quelleKarte.IsFaceup()) return king || Feuer(quelleKarte);
                return Feuer(quelleKarte);
            }
            catch { return false; }
#endif
        }

        /// <summary>Kann ein Effekt dieser Art die Karte vom Feld entfernen (Friedhof, Verbannen, Hand, Deck)? Auch bedingte Immunität
        /// (z. B. Fallen-Paradise-Monster gegen Zauber/Fallen) und in diesem Zug gelernte Ziele zählen als nein. Nicht für Zerstörung (dafür Treffbar/Meiden).</summary>
        internal static bool Entfernbar(ClientCard c, int quelleArt, bool zielt = true, ClientCard quelleKarte = null)
            => c != null && !_gelernt.Contains(c) && !Unberuehrt(c, quelleKarte, quelleArt, false) && !(zielt && KeinZiel(c, quelleArt));

        /// <summary>Kurzform ohne Quellkarte: z. B. Unberuehrt(m, ZerstoerSchutz.AlsZauber).</summary>
        internal static bool Unberuehrt(ClientCard c, int quelleArt, bool strikt = true) => Unberuehrt(c, null, quelleArt, strikt);

        /// <summary>Kann diese Quelle die Karte nicht als Ziel wählen (ohne Bedingung im Kartentext)?</summary>
        internal static bool KeinZiel(ClientCard c, int quelleArt)
            => Wirkt(c) && Suche(KeinZielBits, c, out byte b) && Trifft(b, quelleArt)
               || quelleArt == AlsMonster && KeinMonsterZiel(c)
               || c != null && c.Controller == 1 && EndlessSchutz(c);   // 13.11.: Endless Darkness

        /// <summary>Taugt die Karte als Ziel eines Effekts dieser Art (zielend oder nicht)? Für Aktivierungs-Regeln der KIs.</summary>
        internal static bool Wirksam(ClientCard c, int quelleArt, bool zielt = true, ClientCard quelleKarte = null)
            => c != null && !Unberuehrt(c, quelleKarte, quelleArt, true) && !(zielt && KeinZiel(c, quelleArt));

        private static System.Reflection.FieldInfo _selectorFeld;

        /// <summary>Wie oft eine Vorwahl wegen Immunität getauscht wurde (für Logs der KIs).</summary>
        internal static int Getauscht;

        /// <summary>Vorwahl der KI (AI.SelectCard, auch aus WindBots Default-Funktionen wie DefaultInfiniteImpermanence) ansehen, ohne sie zu verbrauchen.</summary>
        private static WindBot.Game.AI.CardSelector Vorwahl(GameAI ai)
        {
            try
            {
                if (_selectorFeld == null)
                    _selectorFeld = typeof(GameAI).GetField("m_selector", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var liste = _selectorFeld?.GetValue(ai) as IList<WindBot.Game.AI.CardSelector>;
                return liste != null && liste.Count > 0 ? liste[liste.Count - 1] : null;
            }
            catch { return null; }
        }

        /// <summary>Wie Pruefe, prüft aber zusätzlich die Vorwahl: Trifft sie ein Gegnermonster, das gegen diesen Effekt sicher immun ist,
        /// wählt die KI selbst ein anderes Ziel und verbraucht die Vorwahl (sonst rutscht sie in die nächste Auswahl).</summary>
        /// <summary>Karten, deren Aktivierung abgebrochen wurde, weil jedes Ziel immun war, mit den Zielen von damals.</summary>
        private static readonly Dictionary<ClientCard, HashSet<ClientCard>> _sperre = new Dictionary<ClientCard, HashSet<ClientCard>>();
        internal static int Abgebrochen;

        /// <summary>Für OnPreActivate: Karte nicht erneut aktivieren, solange kein neues offenes Gegnermonster dazugekommen ist.</summary>
        internal static bool Gesperrt(ClientCard karte, IEnumerable<ClientCard> gegnerMonster)
        {
            if (karte == null || !_sperre.TryGetValue(karte, out var alt)) return false;
            if (gegnerMonster.Any(m => m != null && m.IsFaceup() && !alt.Contains(m))) { _sperre.Remove(karte); return false; }
            return true;
        }

        private static void SperreLeeren() => _sperre.Clear();

        private static readonly HashSet<object> _umhuellt = new HashSet<object>();

        private static int SelectorAnzahl(GameAI ai)
        {
            try
            {
                if (_selectorFeld == null)
                    _selectorFeld = typeof(GameAI).GetField("m_selector", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                return (_selectorFeld?.GetValue(ai) as IList<WindBot.Game.AI.CardSelector>)?.Count ?? 0;
            }
            catch { return 0; }
        }

        /// <summary>Alle Aktivierungsregeln der KI umhüllen (einmal je KI, aus OnNewTurn): Wählt eine Regel per AI.SelectCard nur Gegnermonster vor,
        /// die gegen alle Karteneffekte immun sind (Ra, Liger, Servant im eigenen Zug …), wird die Aktivierung verworfen, die Vorwahl
        /// zurückgenommen und die Karte gesperrt, bis ein neues Gegnermonster erscheint.</summary>
        internal static void Umhuellen(WindBot.Game.AI.Executor ex, GameAI ai, System.Func<IEnumerable<ClientCard>> gegner, System.Func<ClientCard> karte)
        {
            if (ex == null || ai == null) return;
            _duel = ex.Duel;
            GedaechtnisEinhaengen(ai, ex.Duel);   // 20.11.: verdeckte, vorher offene Karten merken
            if (!_umhuellt.Add(ex)) return;
            var liste = ex.Executors;
#if !OHNE_ANGRIFFSWAHL
            liste.Insert(0, new WindBot.Game.AI.CardExecutor(WindBot.Game.AI.ExecutorType.GoToEndPhase, 0, NachAbbruch));
            liste.Insert(0, new WindBot.Game.AI.CardExecutor(WindBot.Game.AI.ExecutorType.GoToMainPhase2, 0, NachAbbruch));
#endif
            for (int i = 0; i < liste.Count; i++)
            {
                var e = liste[i];
                if (e.Type != WindBot.Game.AI.ExecutorType.Activate || e.Func == null) continue;
                var f = e.Func;
                liste[i] = new WindBot.Game.AI.CardExecutor(e.Type, e.CardId, () =>
                {
                    int vorher = SelectorAnzahl(ai);
                    if (!f()) return false;
                    try
                    {
                        var feld = gegner().Where(m => m != null && m.Location == CardLocation.MonsterZone).ToList();
                        if (feld.Count == 0) return true;
                        var sel = (_selectorFeld?.GetValue(ai) as IList<WindBot.Game.AI.CardSelector>);
                        if (sel == null || sel.Count <= vorher) return true;
                        for (int s = vorher; s < sel.Count; s++)
                        {
                            var treffer = sel[s].Select(feld, 0, 99);
                            if (treffer.Count > 0 && treffer.All(m => Unberuehrt(m, null, Unbekannt, true)))
                            {
                                while (sel.Count > vorher) sel.RemoveAt(sel.Count - 1);
                                var k = karte();
                                if (k != null) _sperre[k] = new HashSet<ClientCard>(feld);
                                Abgebrochen++;
                                return false;
                            }
                        }
                    }
                    catch { }
                    return true;
                });
            }
            UmhuellenNibiru(ex, karte);   // 20.11.: zuletzt, damit die Immunitätsprüfung nur die alten Regeln umhüllt
        }

        /// <param name="aktiv">Die Karte, die die KI zuletzt aktivieren wollte (Executor.Card): beim Abbruch wird sie gesperrt.</param>
        internal static IList<ClientCard> Pruefe(IList<ClientCard> gewaehlt, IList<ClientCard> cards, int min, int max, long hint, GameAI ai, bool abbrechbar, ClientCard aktiv, ClientCard quelleKarte, System.Func<ClientCard, int> wert = null)
        {
            // Jedes wählbare Ziel ist ein Gegnermonster, das gegen alle Karteneffekte immun ist (Ra, Servant im eigenen Zug …):
            // Aktivierung abbrechen statt den Effekt zu verschwenden (nur wenn der Server Abbrechen erlaubt).
            // Auch bei gemischter Liste (Impermanence darf eigene Monster wählen), wenn die KI selbst nur Gegnerkarten gewählt hat.
#if !OHNE_ANGRIFFSWAHL
            if (hint == HintMsg.AttackTarget)
            {
                var a = AngriffsZiel(gewaehlt, cards, min, ai, abbrechbar, aktiv);
                if (a != null || gewaehlt == null) return a;
            }
#endif
            var gegnerK = cards.Where(c => c.Controller == 1).ToList();
            bool zielGegner = cards.All(c => c.Controller == 1) || gewaehlt != null && gewaehlt.Count > 0 && gewaehlt.All(c => c.Controller == 1);
            if (abbrechbar && WirkHinweise.Contains(hint) && gegnerK.Count > 0 && zielGegner
                && gegnerK.All(c => Unberuehrt(c, null, Unbekannt, true)))
            {
                if (ai != null && ai.HaveSelectedCards() && Vorwahl(ai) != null) ai.GetSelectedCards();
                if (aktiv != null) _sperre[aktiv] = new HashSet<ClientCard>(cards);
                Abgebrochen++;
                return new List<ClientCard>();
            }
            bool vorwahl = ai != null && ai.HaveSelectedCards();
            if (gewaehlt == null && vorwahl && WirkHinweise.Contains(hint))
            {
                var sel = Vorwahl(ai);
                if (sel != null)
                {
                    var vor = sel.Select(cards, min, max);
                    int q = Art(quelleKarte);
                    bool weg = Entfernt(hint) || hint == HintMsg.Destroy;
                    bool immun(ClientCard c) => c.Controller == 1 && (Unberuehrt(c, quelleKarte, q, true) || weg && _gelernt.Contains(c));
                    if (!vor.Any(immun))
                    {
                        // Vorwahl bleibt: ihre Ziele merken, damit die KI aus einem verpufften Effekt lernt (10.11.)
                        if (weg) Merke(vor, hint == HintMsg.Destroy);
                        SperreWennVergeblich(vor, cards, quelleKarte, q, weg);
                    }
                    else
                    {
                        ai.GetSelectedCards();
                        int w(ClientCard c) => wert != null ? wert(c) : (c.Location == CardLocation.MonsterZone ? System.Math.Max(c.Attack, 1) : 500);
                        var neu = vor.Where(c => !immun(c)).ToList();
                        foreach (var e in cards.Where(c => c.Controller == 1 && !neu.Contains(c) && !Unberuehrt(c, quelleKarte, q, false)).OrderByDescending(w))
                            if (neu.Count < vor.Count) neu.Add(e);
                        foreach (var e in cards.Where(c => c.Controller == 1 && !neu.Contains(c) && !immun(c)).OrderByDescending(w))
                            if (neu.Count < vor.Count) neu.Add(e);
                        if (neu.Count < min) foreach (var rest in vor) if (neu.Count < min && !neu.Contains(rest)) neu.Add(rest);
                        Getauscht++;
                        var getauscht = Pruefe(neu, cards, min, max, hint, false, quelleKarte, wert);
                        SperreWennVergeblich(getauscht, cards, quelleKarte, q, weg);
                        return getauscht;
                    }
                }
            }
            var erg = Pruefe(gewaehlt, cards, min, max, hint, vorwahl, quelleKarte, wert);
            if (erg != null && WirkHinweise.Contains(hint))
                SperreWennVergeblich(erg, cards, quelleKarte, Art(quelleKarte), Entfernt(hint) || hint == HintMsg.Destroy);
            return erg;
        }

        /// <summary>13.11. (Ketos Blue-Eyes-Duelle 19:42/19:50): Beim Angriffsziel (549) fragt der Kern nur die erlaubten Ziele ab.
        /// Sperrt eine Karte die übrigen (Dragon Master Magia) oder nahm ein eigener Auswahlzweig „stärkstes Gegnermonster“,
        /// griff Salamangreat Balelynx (500 ATK) Magia (5000) bzw. Hyper Ultimate an. Jetzt: nur ein Ziel, das der Angreifer schlägt
        /// (verdeckt zählt als schlagbar); sonst Angriff abbrechen und die Battle Phase beenden (sonst wählt WindBot dasselbe wieder).
        /// Rückgabe null = Auswahl der KI bzw. WindBots Vorwahl bleibt.</summary>
        private static IList<ClientCard> AngriffsZiel(IList<ClientCard> gewaehlt, IList<ClientCard> cards, int min, GameAI ai, bool abbrechbar, ClientCard angreifer)
        {
            try
            {
                if (angreifer == null || angreifer.Controller != 0 || angreifer.Location != CardLocation.MonsterZone || cards.Count == 0) return null;
                // Sun God of Darkness als Angreifer: bekommt bei der Angriffserklärung LP als ATK → nie eingreifen (A/B 13.11.: SUN GOD 70,5 → 66 %)
                if (angreifer.Id == 101403030 || angreifer.Id == 101403130 || angreifer.Name == "The Sun God of Darkness - The Winged Dragon of Ra") return null;
                int atk = angreifer.Attack;
                // Sun God of Darkness: bei Angriffserklärung LP → ATK, jeder Angriff verliert (BLUE-EYES AI 13.11.: 8399 Schaden)
                bool falle(ClientCard c) => c.IsFaceup() && !c.IsDisabled() && (c.Id == 101403030 || c.Id == 101403130 || c.Name == "The Sun God of Darkness - The Winged Dragon of Ra");
                // 20.11.: verdeckt, aber bekannt (vorher offen gesehen) → mit der Original-VER rechnen (verdeckt fallen alle Boni weg)
                bool schlagbar(ClientCard c) => !falle(c) && (c.IsFacedown() ? (!BekanntVerdeckt(c) || atk > c.Data.Defense)
                    : atk > c.GetDefensePower() || atk == c.GetDefensePower() && c.IsAttack() && atk > 0);
                IList<ClientCard> wahl = gewaehlt;
                bool vorwahl = gewaehlt == null && ai != null && ai.HaveSelectedCards();
                if (wahl == null && vorwahl) { var sel = Vorwahl(ai); if (sel != null) wahl = sel.Select(cards, 1, 1); }
                if (wahl != null && wahl.Count > 0 && wahl.All(c => cards.Contains(c) && schlagbar(c))) return gewaehlt;   // passt
                var gut = cards.Where(schlagbar).OrderByDescending(c => c.IsFacedown() ? (BekanntVerdeckt(c) ? c.Data.Defense : 0) : c.GetDefensePower()).ToList();
                if (gut.Count > 0)
                {
                    if (vorwahl) ai.GetSelectedCards();
                    AngriffGetauscht++;
                    return new List<ClientCard> { gut[0] };
                }
                if (!abbrechbar) return null;
                if (vorwahl) ai.GetSelectedCards();
                _angriffAbgebrochen = true;
                AngriffAbgebrochen++;
                return new List<ClientCard>();
            }
            catch { return null; }
        }

        private static bool _angriffAbgebrochen;
        internal static int AngriffAbgebrochen, AngriffGetauscht;

        /// <summary>Nach einem abgebrochenen Angriff: einmal direkt in Main Phase 2 bzw. End Phase (WindBot würde denselben Angriff wieder wählen).</summary>
        private static bool NachAbbruch()
        {
            if (!_angriffAbgebrochen) return false;
            _angriffAbgebrochen = false;
            return true;
        }

        /// <summary>10.11.: Musste die KI trotz Prüfung nur Gegnermonster wählen, an denen der Effekt sicher verpufft (kein Abbruch möglich, Kosten schon bezahlt),
        /// wird die Quelle auf dem Feld gesperrt, bis ein neues Gegnermonster erscheint. Sonst wiederholt sie die Aktivierung (Phoenix ohne „einmal pro Zug“).</summary>
        private static void SperreWennVergeblich(IList<ClientCard> gewaehlt, IList<ClientCard> cards, ClientCard quelleKarte, int q, bool weg)
        {
#if !OHNE_LERNEN
            if (gewaehlt == null || gewaehlt.Count == 0 || quelleKarte == null) return;
            if (quelleKarte.Location != CardLocation.MonsterZone && quelleKarte.Location != CardLocation.SpellZone) return;
            if (!gewaehlt.All(c => c.Controller == 1 && c.Location == CardLocation.MonsterZone && (Unberuehrt(c, quelleKarte, q, true) || weg && _gelernt.Contains(c)))) return;
            _sperre[quelleKarte] = new HashSet<ClientCard>(cards.Where(c => c.Controller == 1));
            Abgebrochen++;
#endif
        }

        /// <summary>Hinweise, bei denen eine Auswahl von Gegnerkarten den Effekt auf sie anwendet.</summary>
        private static readonly HashSet<long> WirkHinweise = new HashSet<long>
        {
            HintMsg.Destroy, HintMsg.Remove, HintMsg.ToGrave, HintMsg.ReturnToHand, HintMsg.ToDeck, HintMsg.Faceup, HintMsg.Control,
            HintMsg.PosChange, HintMsg.Target, 573 /* Disable, fehlt in EDOPros ExecutorBase */, HintMsg.Negate, HintMsg.Attack, HintMsg.Defense, HintMsg.Facedown,
            HintMsg.FaceupAttack, HintMsg.FaceupDefense, HintMsg.FacedownDefense
        };
    }
}
