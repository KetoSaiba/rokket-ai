using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using YGOSharp.OCGWrapper.Enums;
using WindBot;
using WindBot.Game;

namespace WindBot.Game.AI.Decks
{
    /// <summary>Keto 20.11.: „die KI sollte sich merken, welche Karten verdeckt sind, wenn sie vorher offen da lagen.“
    /// Kommt eine bekannte Karte (offen auf dem Feld, im Friedhof oder offen verbannt) verdeckt aufs Feld – z. B. aus dem Friedhof
    /// gesetzt oder in eine andere Zone verdeckt verschoben –, schickt der Server die Kartennummer als 0, und WindBot löscht in OnMove
    /// die Kennung der Karte (card.SetId(0)). Ein Mensch am Tisch weiß aber, was dort liegt. Das Gedächtnis hängt sich per Reflection
    /// vor WindBots MSG_MOVE-Behandlung und setzt die alte Kennung wieder ein. Book of Moon u. ä. (MSG_POS_CHANGE) behält die Kennung
    /// in WindBot schon. Werden verdeckte Karten gemischt (MSG_SHUFFLE_SET_CARD), ist das Wissen zu Recht weg.
    /// 20.11. abends: auch Karten, die offen gesehen auf die Gegnerhand zurückgehen (Bounce, aus Friedhof/Verbannt/Extra Deck geholt).
    /// Karten, die ins Deck gemischt werden, bleiben unbekannt.</summary>
    internal static partial class ZerstoerSchutz
    {
        private static readonly HashSet<object> _gedaechtnisHaken = new HashSet<object>();

        /// <summary>Wie oft die KI sich in diesem Prozess eine verdeckte Karte gemerkt hat (für Logs/Tests).</summary>
        internal static int Gemerkt;

        /// <summary>Verdeckte Gegnerkarte, deren Identität die KI kennt (vorher offen gesehen).</summary>
        internal static bool BekanntVerdeckt(ClientCard c) => c != null && c.IsFacedown() && c.Id != 0 && c.Data != null;

        /// <summary>Einmal pro Duell-Verbindung aus Umhuellen aufgerufen.</summary>
        internal static void GedaechtnisEinhaengen(GameAI ai, Duel duel)
        {
#if !OHNE_GEDAECHTNIS
            try
            {
                const BindingFlags bf = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
                object client = null;
                foreach (FieldInfo f in typeof(GameAI).GetFields(bf))
                {
                    Delegate d = f.GetValue(ai) as Delegate;
                    if (d != null && d.Target != null && d.Target.GetType().Name == "GameClient") { client = d.Target; break; }
                }
                if (client == null) return;
                FieldInfo behaviorField = client.GetType().GetField("_behavior", bf);
                object behavior = behaviorField != null ? behaviorField.GetValue(client) : null;
                if (behavior != null) GedaechtnisEinhaengen(behavior, duel);
            }
            catch (Exception) { }
#endif
        }

        /// <summary>Wissen über die Gegnerhand für eine Duell-Verbindung (20.11.: Keto „merk dir auch Karten, die auf die Hand zurückgehen“).</summary>
        internal sealed class HandWissen
        {
            /// <summary>Kennungen bekannter Karten in der Gegnerhand, älteste zuerst (eine Kennung pro Karte, doppelt möglich).</summary>
            public readonly List<int> Bekannt = new List<int>();
            /// <summary>Handkarten-Objekte, denen das Gedächtnis eine Kennung gegeben hat (WindBot kennt die Reihenfolge der Gegnerhand nicht).</summary>
            public readonly HashSet<ClientCard> Zugewiesen = new HashSet<ClientCard>();
            /// <summary>Eigene Normal-/Spezialbeschwörungen in diesem Zug (für Nibiru).</summary>
            public int Beschwoerungen;
            /// <summary>Testschalter KI_GEGNERHAND_TEST schon angewendet (nur Simulator).</summary>
            public bool TestGesetzt;
        }

        /// <summary>Handwissen je laufendem Duell, damit Regeln (Nibiru-Bremse) es finden.</summary>
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Duel, HandWissen> _handJeDuell
            = new System.Runtime.CompilerServices.ConditionalWeakTable<Duel, HandWissen>();

        /// <summary>Nibiru, the Primal Being (+ Alternativ-Artwork).</summary>
        private static readonly HashSet<int> NibiruIds = new HashSet<int> { 27204311, 27204313 };

        /// <summary>Wie oft die Nibiru-Bremse eine Beschwörung/Aktivierung verhindert hat (für Logs/Tests).</summary>
        internal static int NibiruGebremst;

        /// <summary>Weiß die KI, dass der Gegner diese Karte (oder ihr Alternativ-Artwork) auf der Hand hat?</summary>
        internal static bool GegnerHatAufHand(Duel duel, ICollection<int> ids)
        {
            try
            {
                if (duel == null) return false;
                HandWissen w;
                if (_handJeDuell.TryGetValue(duel, out w) && w.Bekannt.Exists(ids.Contains)) return true;
                foreach (var c in duel.Fields[1].Hand) if (c != null && (ids.Contains(c.Id) || ids.Contains(c.Alias))) return true;
            }
            catch (Exception) { }
            return false;
        }

        /// <summary>20.11. (Keto: „Handfallen beachten“): Kennt die KI Nibiru auf der Gegnerhand und hat in diesem Zug schon
        /// 4 Monster beschworen, beschwört sie in der Main Phase nichts mehr – die 5. Beschwörung würde Nibiru erlauben,
        /// das ganze Feld zu opfern. Gilt nur bei sicherem Wissen (Karte vorher offen gesehen), nie auf Verdacht.</summary>
        internal static bool NibiruBremse(Duel duel)
        {
#if OHNE_NIBIRUBREMSE
            return false;
#else
            try
            {
                if (duel == null || (duel.Phase != DuelPhase.Main1 && duel.Phase != DuelPhase.Main2)) return false;
                HandWissen w;
                if (!_handJeDuell.TryGetValue(duel, out w) || w.Beschwoerungen < 4) return false;
                return GegnerHatAufHand(duel, NibiruIds);
            }
            catch (Exception) { return false; }
#endif
        }

        /// <summary>Hängt die Nibiru-Bremse vor jede Beschwörungsregel und vor jede eigene Aktivierung mit „Summon“ im Text
        /// (nur aus eigener Initiative, also ohne laufende Kette – Antworten auf Gegnereffekte bleiben erlaubt).</summary>
        internal static void UmhuellenNibiru(WindBot.Game.AI.Executor ex, Func<ClientCard> karte)
        {
#if !OHNE_NIBIRUBREMSE
            try
            {
                var liste = ex.Executors;
                for (int i = 0; i < liste.Count; i++)
                {
                    var e = liste[i];
                    bool besch = e.Type == WindBot.Game.AI.ExecutorType.Summon || e.Type == WindBot.Game.AI.ExecutorType.SpSummon
                              || e.Type == WindBot.Game.AI.ExecutorType.SummonOrSet;
                    if (!besch && e.Type != WindBot.Game.AI.ExecutorType.Activate) continue;
                    var f = e.Func;
                    liste[i] = new WindBot.Game.AI.CardExecutor(e.Type, e.CardId, () =>
                    {
                        try
                        {
                            if (NibiruBremse(ex.Duel) && (besch || ex.Duel.CurrentChain.Count == 0 && BeschwoertVielleicht(karte())))
                            {
                                NibiruGebremst++;
                                return false;
                            }
                        }
                        catch (Exception) { }
                        return f == null || f();
                    });
                }
            }
            catch (Exception) { }
#endif
        }

        /// <summary>Kann diese eigene Karte mit ihrer Aktivierung etwas beschwören? (Text enthält „Summon“, grob, aber sicher.)</summary>
        internal static bool BeschwoertVielleicht(ClientCard c)
        {
            try { return c != null && c.Data != null && c.Data.Description != null && c.Data.Description.Contains("Summon"); }
            catch (Exception) { return false; }
        }

        /// <summary>Wie oft eine bekannte Karte auf die Gegnerhand ging (für Logs/Tests).</summary>
        internal static int HandGemerkt;

        /// <summary>Ersetzt WindBots MSG_MOVE-, MSG_SHUFFLE_HAND- und MSG_START-Behandlung in diesem GameBehavior.</summary>
        internal static bool GedaechtnisEinhaengen(object behavior, Duel duel)
        {
            try
            {
                if (behavior == null || duel == null) return false;
                lock (_gedaechtnisHaken) { if (!_gedaechtnisHaken.Add(behavior)) return true; }
                const BindingFlags bf = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
                FieldInfo messagesField = behavior.GetType().GetField("_messages", bf);
                var messages = messagesField != null ? messagesField.GetValue(behavior) as IDictionary<GameMessage, Action<BinaryReader>> : null;
                Action<BinaryReader> original;
                if (messages == null || !messages.TryGetValue(GameMessage.Move, out original) || original == null) return false;
                var hand = new HandWissen();
                try { _handJeDuell.Remove(duel); _handJeDuell.Add(duel, hand); } catch (Exception) { }
                messages[GameMessage.Move] = packet => BewegungMitGedaechtnis(packet, original, duel, hand);
#if !OHNE_HANDGEDAECHTNIS
                Action<BinaryReader> mischen, start, zeigen, neuerZug, besch, spBesch;
                if (messages.TryGetValue(GameMessage.ShuffleHand, out mischen) && mischen != null)
                    messages[GameMessage.ShuffleHand] = packet => { mischen(packet); HandAbgleichen(duel, hand); };
                if (messages.TryGetValue(GameMessage.Start, out start) && start != null)
                    messages[GameMessage.Start] = packet => { hand.Bekannt.Clear(); hand.Zugewiesen.Clear(); hand.Beschwoerungen = 0; hand.TestGesetzt = false; start(packet); };
                // vorgezeigte Handkarten (gesuchte Karten, „zeige eine Karte aus deiner Hand“) bleiben auch nach dem Mischen bekannt
                // EDOPros WindBot (Juli 2025) hat für MSG_CONFIRM_CARDS gar keine Behandlung → dann übernimmt das Gedächtnis sie ganz
                messages.TryGetValue(GameMessage.ConfirmCards, out zeigen);
                messages[GameMessage.ConfirmCards] = packet => VorzeigenMitGedaechtnis(packet, zeigen, duel, hand);
                if (messages.TryGetValue(GameMessage.NewTurn, out neuerZug) && neuerZug != null)
                    messages[GameMessage.NewTurn] = packet => { hand.Beschwoerungen = 0; TestHand(duel, hand); neuerZug(packet); };
                if (messages.TryGetValue(GameMessage.Summoning, out besch) && besch != null)
                    messages[GameMessage.Summoning] = packet => { ZaehleBeschwoerung(packet, duel, hand); besch(packet); };
                if (messages.TryGetValue(GameMessage.SpSummoning, out spBesch) && spBesch != null)
                    messages[GameMessage.SpSummoning] = packet => { ZaehleBeschwoerung(packet, duel, hand); spBesch(packet); };
#endif
                return true;
            }
            catch (Exception) { return false; }
        }

        /// <summary>MSG_SUMMONING / MSG_SPSUMMONING: Karten-ID (4), Spieler (1), Ort (1), Zone (4), Position (4). Zählt eigene Beschwörungen.</summary>
        private static void ZaehleBeschwoerung(BinaryReader packet, Duel duel, HandWissen w)
        {
            try
            {
                long start = packet.BaseStream.Position;
                packet.ReadInt32();
                int spieler = packet.ReadByte();
                packet.BaseStream.Position = start;
                if (!duel.IsFirst) spieler = 1 - spieler;
                if (spieler == 0) w.Beschwoerungen++;
            }
            catch (Exception) { }
        }

        /// <summary>Nur für Simulator-Tests: Umgebungsvariable KI_GEGNERHAND_TEST = Kennungen (mit Komma), die die KI ab dem
        /// ersten Zug als „bekannt auf der Gegnerhand“ behandelt. In EDOPro nie gesetzt.</summary>
        private static void TestHand(Duel duel, HandWissen w)
        {
            try
            {
                if (w.TestGesetzt) return;
                string t = Environment.GetEnvironmentVariable("KI_GEGNERHAND_TEST");
                if (string.IsNullOrEmpty(t)) { w.TestGesetzt = true; return; }
                if (duel.Fields[1].Hand.Count < 4) return;
                foreach (var s in t.Split(',')) { int id; if (int.TryParse(s.Trim(), out id) && id > 0) w.Bekannt.Add(id); }
                w.TestGesetzt = true;
                HandAbgleichen(duel, w);
            }
            catch (Exception) { }
        }

        /// <summary>MSG_CONFIRM_CARDS: Spieler (1), Anzahl (4), je Karte: ID (4), Spieler (1), Ort (1), Zone (4).
        /// Eine vorgezeigte Gegner-Handkarte kommt ins Handwissen, wenn sie dort noch nicht gezählt ist.</summary>
        private static void VorzeigenMitGedaechtnis(BinaryReader packet, Action<BinaryReader> original, Duel duel, HandWissen w)
        {
            var gezeigt = new List<ClientCard>();
            var gezeigtIds = new Dictionary<ClientCard, int>();
            try
            {
                long start = packet.BaseStream.Position;
                packet.ReadByte();
                int anzahl = packet.ReadInt32();
                for (int i = 0; i < anzahl && i < 60; i++)
                {
                    int id = packet.ReadInt32(); int spieler = packet.ReadByte(); int ort = packet.ReadByte(); int zone = packet.ReadInt32();
                    if (!duel.IsFirst) spieler = 1 - spieler;
                    // ohne WindBot-Behandlung: vorgezeigte verdeckte Feldkarten selbst eintragen (wie neuere WindBots)
                    if (original == null && id > 0 && (ort == (int)CardLocation.MonsterZone || ort == (int)CardLocation.SpellZone))
                    {
                        var f = duel.GetCard(spieler, (CardLocation)ort, zone);
                        if (f != null && f.Id == 0) f.SetId(id);
                    }
                    if (id <= 0 || spieler != 1 || ort != (int)CardLocation.Hand) continue;
                    var c = duel.GetCard(1, CardLocation.Hand, zone);
                    if (c == null || gezeigt.Contains(c)) continue;
                    gezeigt.Add(c);
                    gezeigtIds[c] = id;
                    // schon gezählt: diese Handkarte trägt die Kennung bereits (früher vorgezeigt oder vom Gedächtnis richtig zugeordnet)
                    // liegt dieselbe Kennung nur zugeordnet auf einer anderen Handkarte, ist es dieselbe Karte (nicht doppelt zählen)
                    bool zugeordnet = false;
                    foreach (var z in w.Zugewiesen) if (z != c && z.Id == id && z.Location == CardLocation.Hand) { zugeordnet = true; break; }
                    if (c.Id != id && !zugeordnet) w.Bekannt.Add(id);
                }
                packet.BaseStream.Position = start;
            }
            catch (Exception) { gezeigt.Clear(); }

            if (original != null) original(packet);

            if (gezeigt.Count == 0) return;
            try
            {
                foreach (var c in gezeigt) w.Zugewiesen.Remove(c);
                if (original == null)
                    foreach (var c in gezeigt) if (gezeigtIds.ContainsKey(c)) c.SetId(gezeigtIds[c]);   // jetzt sicher bekannt, nicht mehr nur zugeordnet
                HandAbgleichen(duel, w);
            }
            catch (Exception) { }
        }

        /// <summary>Verteilt die bekannten Kennungen neu auf die Gegnerhand: erst alte Zuweisungen löschen, dann jede bekannte Kennung,
        /// die nicht schon offen in der Hand steht (z. B. vorgezeigt), auf eine unbekannte Handkarte legen.</summary>
        internal static void HandAbgleichen(Duel duel, HandWissen w)
        {
            try
            {
                var hand = duel.Fields[1].Hand;
                foreach (var c in w.Zugewiesen)
                    if (c != null && c.Location == CardLocation.Hand && hand.Contains(c)) c.SetId(0);
                w.Zugewiesen.Clear();
                // mehr bekannte Karten als Handkarten (Karte verdeckt weggegangen): die ältesten Einträge fallen weg
                while (w.Bekannt.Count > hand.Count) w.Bekannt.RemoveAt(0);
                var fehlt = new List<int>(w.Bekannt);
                foreach (var c in hand) if (c != null && c.Id != 0) fehlt.Remove(c.Id);
                foreach (var c in hand)
                {
                    if (fehlt.Count == 0) break;
                    if (c == null || c.Id != 0) continue;
                    c.SetId(fehlt[0]);
                    fehlt.RemoveAt(0);
                    w.Zugewiesen.Add(c);
                }
            }
            catch (Exception) { }
        }

        /// <summary>MSG_MOVE: Karten-ID (4), vorher: Spieler (1), Ort (1), Zone (4), Position (4), nachher: dasselbe, Grund (4).
        /// Spieler aus Sicht des Servers; WindBot rechnet mit IsFirst um (wie LocationInfo).</summary>
        internal static void BewegungMitGedaechtnis(BinaryReader packet, Action<BinaryReader> original, Duel duel, HandWissen hand = null)
        {
            ClientCard karte = null;
            int alteId = 0;
            int aufHand = 0;          // bekannte Karte geht auf die Gegnerhand
            int vonHand = -1;         // Karte verlässt die Gegnerhand: offen gezeigte Kennung (0 = verdeckt)
            try
            {
                long start = packet.BaseStream.Position;
                int neueId = packet.ReadInt32();
                int vSpieler = packet.ReadByte(); int vOrt = packet.ReadByte(); int vZone = packet.ReadInt32(); int vPos = packet.ReadInt32();
                int nSpieler = packet.ReadByte(); int nOrt = packet.ReadByte(); packet.ReadInt32(); int nPos = packet.ReadInt32();
                packet.BaseStream.Position = start;
                if (!duel.IsFirst) { vSpieler = 1 - vSpieler; nSpieler = 1 - nSpieler; }
                const int Feld = (int)CardLocation.MonsterZone | (int)CardLocation.SpellZone;
                const int Oeffentlich = Feld | (int)CardLocation.Grave | (int)CardLocation.Removed;
                bool ohneXyz = (vOrt & (int)CardLocation.Overlay) == 0 && (nOrt & (int)CardLocation.Overlay) == 0;
                bool vonOeffentlich = ohneXyz && (vOrt & Oeffentlich) != 0;
                bool aufsFeldVerdeckt = ohneXyz && (nOrt & Feld) != 0 && (nPos & (int)CardPosition.FaceDown) != 0;
                if (neueId == 0 && vonOeffentlich && aufsFeldVerdeckt)
                {
                    karte = duel.GetCard(vSpieler, (CardLocation)vOrt, vZone);
                    // Gegnerkarten haben in WindBot nur dann eine Kennung, wenn sie öffentlich waren (offen, Friedhof, Book of Moon …)
                    if (karte != null && karte.Id != 0) alteId = karte.Id;
                    else karte = null;
                }
#if !OHNE_HANDGEDAECHTNIS
                if (hand != null && ohneXyz)
                {
                    bool aufGegnerhand = nSpieler == 1 && (nOrt & (int)CardLocation.Hand) != 0;
                    bool vonGegnerhand = vSpieler == 1 && (vOrt & (int)CardLocation.Hand) != 0;
                    if (aufGegnerhand && !vonGegnerhand && ((vOrt & Oeffentlich) != 0 || (vOrt & (int)CardLocation.Extra) != 0))
                    {
                        var vorher = duel.GetCard(vSpieler, (CardLocation)vOrt, vZone);
                        bool gesehen = vorher != null && vorher.Id != 0 && ((vOrt & (int)CardLocation.Extra) == 0 || vorher.IsFaceup());
                        if (gesehen) aufHand = vorher.Id;
                        else if (neueId != 0) aufHand = neueId;
                    }
                    if (vonGegnerhand && !aufGegnerhand)
                    {
                        vonHand = neueId;
                        // verdeckt weg, aber diese Handkarte war sicher bekannt (gerade vorgezeigt, ohne Mischen dazwischen):
                        // dann weiß die KI, welche Karte ging – auch wenn sie verdeckt gesetzt wird
                        var weg = duel.GetCard(1, CardLocation.Hand, vZone);
                        if (neueId == 0 && weg != null && weg.Id != 0 && !hand.Zugewiesen.Contains(weg))
                        {
                            vonHand = weg.Id;
                            if (aufsFeldVerdeckt) { karte = weg; alteId = weg.Id; }
                        }
                    }
                }
#endif
            }
            catch (Exception) { karte = null; aufHand = 0; vonHand = -1; }

            original(packet);

#if !OHNE_HANDGEDAECHTNIS
            if (hand != null && (aufHand != 0 || vonHand >= 0))
            {
                try
                {
                    if (vonHand > 0) hand.Bekannt.Remove(vonHand);   // offen ausgespielt/abgeworfen: diese Karte ist nicht mehr auf der Hand
                    if (aufHand != 0) { hand.Bekannt.Add(aufHand); HandGemerkt++; }
                    HandAbgleichen(duel, hand);
                }
                catch (Exception) { }
            }
#endif

            if (karte == null || alteId == 0) return;
            try
            {
                if (karte.Id == 0 && (karte.Location == CardLocation.MonsterZone || karte.Location == CardLocation.SpellZone))
                {
                    karte.SetId(alteId);
                    Gemerkt++;
                }
            }
            catch (Exception) { }
        }
    }
}
