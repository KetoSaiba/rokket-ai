using System;
using System.Collections.Generic;
using System.Linq;
using YGOSharp.OCGWrapper.Enums;
using WindBot;
using WindBot.Game;

namespace WindBot.Game.AI.Decks
{
    /// <summary>Gemeinsam für alle KIs (Keto 03.11.: Zerstörungseffekte auf Monster, die nicht zerstört werden können, darf es bei keiner KI geben;
    /// Duell 16:48: Abominable Soul und Wailing verpufften an Borrelend). Die Tabelle kommt aus den Kartentexten (schutz_tabelle.py).
    /// Dazu lernt die KI im Zug: Karte als Zerstörungsziel gewählt, Kette vorbei, Karte steht noch → gilt bis Zugende als geschützt
    /// (Schutz durch andere Karten, Ersatzeffekte wie Enma). WindBot sieht aktive Schutzeffekte nicht.</summary>
    internal static partial class ZerstoerSchutz
    {
        internal const int AlsMonster = 1, AlsZauber = 2, AlsFalle = 4, Unbekannt = 0;

        private static readonly List<ClientCard> _gezielt = new List<ClientCard>();
        private static readonly HashSet<ClientCard> _gelernt = new HashSet<ClientCard>();

        internal static int Art(ClientCard quelle)
        {
            if (quelle == null) return Unbekannt;
            if (quelle.HasType(CardType.Monster)) return AlsMonster;
            if (quelle.HasType(CardType.Spell)) return AlsZauber;
            if (quelle.HasType(CardType.Trap)) return AlsFalle;
            return Unbekannt;
        }

        private static bool Trifft(byte schutz, int quelle) => quelle == Unbekannt ? schutz == 7 : (schutz & quelle) != 0;

        private static int Kennung(ClientCard c)
        {
            int alias = c.Data != null ? c.Data.Alias : 0;
            return alias != 0 ? alias : c.Id;
        }

        private static bool Wirkt(ClientCard c) => c != null && c.IsFaceup() && !c.IsDisabled()
            && (c.Location == CardLocation.MonsterZone || c.Location == CardLocation.SpellZone);

        /// <summary>Laut Kartentext immer geschützt (nicht negiert). Hartes Verbot für Zerstörungseffekte.</summary>
        internal static bool Sicher(ClientCard c, int quelle = Unbekannt)
            => Wirkt(c) && Suche(SicherDaten, c, out byte s) && Trifft(s, quelle) || EndlessSchutz(c);

        /// <summary>13.11. (Ketos Blue-Eyes-Duelle, LUNALIGHT AI zahlte Sabre Dancer für Ligers Massenzerstörung gegen Hyper Ultimate):
        /// Feldzauber „Endless Darkness“ – eigene Monster mit 4000+ Original-ATK können nicht durch Karteneffekte zerstört
        /// und vom Gegner nicht als Ziel gewählt werden.</summary>
        private static readonly HashSet<int> EndlessIds = new HashSet<int> { 101404049, 777200049 };

        internal static bool EndlessSchutz(ClientCard c)
        {
#if OHNE_ENDLESS
            return false;
#else
            try
            {
                if (c == null || _duel == null || c.Location != CardLocation.MonsterZone || !c.IsFaceup()) return false;
                int basis = c.Data != null ? c.Data.Attack : c.BaseAttack;
                if (basis < 4000) return false;
                var f = _duel.Fields[c.Controller].GetFieldSpellCard();
                return f != null && f.IsFaceup() && !f.IsDisabled() && (EndlessIds.Contains(f.Id) || f.Name == "Endless Darkness");
            }
            catch { return false; }
#endif
        }

        /// <summary>Wird diese Zerstörung sicher oder nach Erfahrung dieses Zuges verpuffen?</summary>
        internal static bool Verpufft(ClientCard c, int quelle = Unbekannt) => c != null && (Sicher(c, quelle) || _gelernt.Contains(c) || Unberuehrt(c, null, quelle, true));

        /// <summary>Schutz unter Bedingung (z. B. Borrelcode, solange ein Link auf ihn zeigt): meiden, wenn es ein anderes Ziel gibt.</summary>
        internal static bool Meiden(ClientCard c, int quelle = Unbekannt)
            => Verpufft(c, quelle) || Wirkt(c) && Suche(BedingtDaten, c, out byte s) && Trifft(s, quelle) || Unberuehrt(c, null, quelle, false);

        /// <summary>Monstereffekte können die Karte nicht als Ziel wählen (Borrelend): Der Planer darf sie nicht einplanen.</summary>
        internal static bool KeinMonsterZiel(ClientCard c) => Wirkt(c) && Suche(KeinZielDaten, c);

        /// <summary>Offenes, nicht negiertes Monster, das laut Kartentext negieren kann.</summary>
        internal static bool Negierer(ClientCard c) => Wirkt(c) && Suche(NegiererDaten, c);

        /// <summary>Gibt es unter den Karten mindestens ein Ziel, das die Zerstörung wirklich trifft?</summary>
        internal static bool HatZiel(IEnumerable<ClientCard> karten, int quelle = Unbekannt)
            => karten != null && karten.Any(c => c != null && !Verpufft(c, quelle));

        internal static IEnumerable<ClientCard> Treffbar(IEnumerable<ClientCard> karten, int quelle = Unbekannt)
            => karten.Where(c => c != null && !Verpufft(c, quelle));

        /// <summary>Wie Treffbar, wertet aber Bedingungen aus, die von der Quelle abhängen (ATK-Vergleich bei Divine Anima, FEUER bei Salamangreat Shrine).</summary>
        internal static IEnumerable<ClientCard> Treffbar(IEnumerable<ClientCard> karten, ClientCard quelleKarte)
            => karten.Where(c => c != null && !Verpufft(c, Art(quelleKarte)) && !Unberuehrt(c, quelleKarte, Art(quelleKarte), true));

        /// <summary>Divine Anima und Abyss Vagrada: unbeeinflusst von aktivierten Effekten von Monstern mit weniger ATK (Ketos Cubic-Duelle 04.11., Effekte verpufften an Anima).</summary>
        private static readonly HashSet<int> AtkVergleichDaten = new HashSet<int> { 100460004, 100460003 };

        /// <summary>Wirkt der Monstereffekt dieser Quelle gar nicht auf die Karte? Nur Monster als Quelle.</summary>
        internal static bool AtkImmun(ClientCard c, ClientCard quelleKarte)
            => quelleKarte != null && quelleKarte.HasType(CardType.Monster) && Wirkt(c) && Suche(AtkVergleichDaten, c) && quelleKarte.Attack < c.Attack;

        /// <summary>Am Ende jeder OnSelectCard-Auswahl: geschützte Gegnerkarten bei „Zerstören“ gegen treffbare tauschen und Ziele merken.
        /// gewaehlt == null ist WindBots Standardauswahl; mit Vorwahl (AI.SelectCard) bleibt sie, die prüft die Regel davor.</summary>
        internal static IList<ClientCard> Pruefe(IList<ClientCard> gewaehlt, IList<ClientCard> cards, int min, int max, long hint, bool vorwahl, ClientCard quelleKarte, Func<ClientCard, int> wert = null)
        {
            bool zerstoeren = hint == HintMsg.Destroy;
            bool alt = zerstoeren || hint == HintMsg.Negate || hint == HintMsg.Faceup;
            // 08.11.: Immunität („unaffected by …“) bei allen Hinweisen, die einen Effekt auf die gewählten Gegnerkarten anwenden
            if (!alt && !WirkHinweise.Contains(hint)) return gewaehlt;
            int q = Art(quelleKarte);
            int w(ClientCard c) => wert != null ? wert(c) : (c.Location == CardLocation.MonsterZone ? Math.Max(c.Attack, 1) : 500);
            // 10.11. (Turnier f52): Auch Friedhof/Verbannen/Hand/Deck lernen aus Zielen, die stehen geblieben sind (Schutz durch andere Karten,
            // z. B. Salamangreat Circle: Phoenix der SUN GOD AI zahlte 6× 1000 LP für denselben King of Beasts).
            bool entfernen = zerstoeren || Entfernt(hint);
            bool problem(ClientCard c) => c.Controller == 1 && (zerstoeren && Meiden(c, q) || entfernen && _gelernt.Contains(c) || Unberuehrt(c, quelleKarte, q, false));
            bool verpufft(ClientCard c) => zerstoeren && Verpufft(c, q) || entfernen && _gelernt.Contains(c) || Unberuehrt(c, quelleKarte, q, true);
            if (gewaehlt == null)
            {
                if (vorwahl || !cards.Any(problem))
                {
                    // Standardauswahl ohne Vorwahl nimmt die ersten Karten: als Ziele merken (die Vorwahl merkt die Überladung mit AI)
                    if (!vorwahl && entfernen) Merke(cards.Take(Math.Max(1, min)), zerstoeren);
                    return null;
                }
                // Bei den neuen Hinweisen nur eingreifen, wenn ausschließlich Gegnerkarten zur Wahl stehen (sonst wollte die KI evtl. eigene Karten)
                if (!alt && cards.Any(c => c.Controller == 0)) return null;
                var gut = cards.Where(c => c.Controller == 1 && !problem(c)).OrderByDescending(w).ToList();
                if (gut.Count == 0) gut = cards.Where(c => c.Controller == 1 && !verpufft(c)).OrderByDescending(w).ToList();
                if (gut.Count == 0 && cards.Any(c => c.Controller == 0)) return null;
                if (gut.Count == 0) gut = cards.ToList();
                gewaehlt = gut.Take(Math.Max(1, min)).ToList();
            }
            else if (gewaehlt.Any(problem))
            {
                var neu = gewaehlt.Where(c => !problem(c)).ToList();
                foreach (var ersatz in cards.Where(c => c.Controller == 1 && !neu.Contains(c) && !problem(c)).OrderByDescending(w))
                    if (neu.Count < gewaehlt.Count) neu.Add(ersatz);
                foreach (var ersatz in cards.Where(c => c.Controller == 1 && !neu.Contains(c) && !verpufft(c)).OrderByDescending(w))
                    if (neu.Count < gewaehlt.Count) neu.Add(ersatz);
                if (neu.Count < min) foreach (var rest in gewaehlt) if (neu.Count < min && !neu.Contains(rest)) neu.Add(rest);
                gewaehlt = neu;
            }
            if (entfernen) Merke(gewaehlt, zerstoeren);
            return gewaehlt;
        }

        /// <summary>Für Massenzerstörung ohne Auswahl (Liger, Raviel): Die Regel meldet beim Aktivieren, was getroffen werden soll.
        /// Was nach der Kette noch steht, gilt bis Zugende als geschützt (ein zweiter Liger zahlt dann nicht noch einmal).</summary>
        internal static void MerkeZiele(IEnumerable<ClientCard> ziele) { if (ziele != null) Merke(ziele.ToList(), true); }

        /// <summary>Trap-Monster stehen nicht in der Tabelle aus den Monstertexten (Turnier f52: Raviel und Liger verpufften am Egyptian God Slime).</summary>
        static ZerstoerSchutz() { SicherDaten[101403073] = 7; }

        /// <summary>Hinweise außer „Zerstören“, nach denen eine getroffene Gegnerkarte das Feld verlässt.</summary>
        private static bool Entfernt(long hint)
        {
#if OHNE_LERNEN
            return false;
#else
            return hint == HintMsg.ToGrave || hint == HintMsg.Remove || hint == HintMsg.ReturnToHand || hint == HintMsg.ToDeck;
#endif
        }

        /// <summary>Ziele, die das Feld nur über andere Wege verlassen sollten (Friedhof, Verbannen, Hand, Deck): gelernt wird nur ohne Antwort des Gegners.</summary>
        private static readonly List<ClientCard> _gezieltWeg = new List<ClientCard>();

        private static void Merke(IEnumerable<ClientCard> ziele, bool zerstoeren)
        {
            foreach (var c in ziele)
                if (c != null && c.Controller == 1 && (c.Location == CardLocation.MonsterZone || c.Location == CardLocation.SpellZone))
                    (zerstoeren ? _gezielt : _gezieltWeg).Add(c);
        }

        /// <summary>In OnChainEnd aufrufen: Was Ziel einer eigenen Zerstörung war und noch offen auf dem Feld steht, ist bis Zugende geschützt.</summary>
        internal static void KettenEnde()
        {
            foreach (var c in _gezielt)
                if (c.Controller == 1 && c.IsFaceup() && (c.Location == CardLocation.MonsterZone || c.Location == CardLocation.SpellZone)) _gelernt.Add(c);
            _gezielt.Clear();
            // Neue Hinweise: Hat der Gegner als Letzter an die Kette gehängt, kann er den Effekt auch nur annulliert haben → nichts lernen.
            bool antwort = false;
            try
            {
                var kette = _duel?.CurrentChain;
                if (kette != null && kette.Count > 0) { var l = kette[kette.Count - 1]; antwort = l != null && l.Controller == 1; }
            }
            catch { }
            if (!antwort)
                foreach (var c in _gezieltWeg)
                    if (c.Controller == 1 && c.IsFaceup() && (c.Location == CardLocation.MonsterZone || c.Location == CardLocation.SpellZone)) _gelernt.Add(c);
            _gezieltWeg.Clear();
        }

        /// <summary>In OnNewTurn aufrufen.</summary>
        internal static void NeuerZug(int amZug = -1) { _gezielt.Clear(); _gezieltWeg.Clear(); _gelernt.Clear(); _amZug = amZug; SperreLeeren(); _angriffAbgebrochen = false; }
    }
}
