using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace WindBot.Game.AI.Decks
{
    /// <summary>Kartenarten des Decks. Reihenfolge: Main-Deck-Monster, Zauber/Fallen, Extra Deck.</summary>
    internal enum K : byte
    {
        Barrage, Detonator, Loader, Tracer, Absorouter, Auto, Magnamhut, Magna, Nocto, Caliber, Recharger, Synchron, Shel, Silver,
        Droplet, Launch, Triple, Boot, Regen, Interlock, Rapid, Imperm, Reboot,
        Striker, Pisty, Twin, Breach, Dilling, Seal, Tris, Code, Sword, Liberator, Zero, End, Shroud, Fatal, Furious, Anderes, N
    }

    /// <summary>Wo eine Karte liegt, die ein Plan-Schritt auswählt.</summary>
    internal enum O : byte { Hand, Deck, Grab, Monster, Zone, GegnerM, GegnerZ, Extra, Verbannt, FeldZauber }

    internal struct Wahl
    {
        public K Karte; public O Ort; public short Gegner; public byte Flags;   // Flags: welche von zwei gleichen Karten (negiert, geht in der End Phase)
        public Wahl(K k, O o, int gegner = -1, byte flags = 0) { Karte = k; Ort = o; Gegner = (short)gegner; Flags = flags; }
        public override string ToString() => Gegner >= 0 ? $"Gegner#{Gegner}" : $"{Karte}@{Ort}";
    }

    internal enum Art : byte { Normal, Proc, Aktivieren, Link, Ausloeser, Auslassen }

    /// <summary>Ein Schritt des Plans: Art, Karte, Ort der Aktivierung und die Auswahl in Reihenfolge der Abfragen.</summary>
    internal sealed class Schritt
    {
        public Art Art; public K Karte; public O Ort; public int Gruppe;
        public Wahl[] Wahlen = Array.Empty<Wahl>();
        public sbyte Option = -1;    // Triple Borrel: 1. oder 2. Effekt; Boot Sector: 0 Hand, 1 Friedhof
        public sbyte Zone = -1;      // Zielzone der Beschwörung, wenn sie zählt
        public bool Ziehen;          // Karte gezogen: danach neu planen
        public override string ToString() => $"{Art} {Karte}@{Ort}" + (Wahlen.Length > 0 ? " [" + string.Join(", ", Wahlen) + "]" : "") + (Option >= 0 ? $" Opt{Option}" : "") + (Zone >= 0 ? $" Z{Zone}" : "") + (Gruppe > 0 ? $" G{Gruppe}" : "");
    }

    /// <summary>Gegnerkarte für den Planer.</summary>
    internal sealed class GegnerKarte { public int Id, Wert, Atk; public bool Offen, Immun, Unzerst, Monster, Verteidigung, Negiert; }

    /// <summary>Zugplaner für Ketos Rokkets: Ziel ist Borrelend + Borrelshroud auf dem Feld (Keto 03.11.). Modelliert Zonen und Link-Pfeile, weil Link-Monster
    /// nur in die Extra-Monsterzone oder in Zonen dürfen, auf die ein Link zeigt. Strahlsuche wie bei Unchained; stimmt das echte Spiel nicht mehr mit
    /// dem Plan überein (Handfalle, gezogene Karte), plant die KI vom echten Zustand neu.</summary>
    internal sealed class Planer
    {
        internal const int NK = (int)K.N;
        internal enum E
        {
            BarrageHand, BarrageZ, DetProc, DetEff, LoaderS, TracerEff, AbsProc, AbsS, MagnamHand, MagnaZ, NoctoH, NoctoD, CaliberProc, CaliberTrib, ShelZ, SilverZ, AutoZ,
            TripleA, TripleB, BootEff, RegenAkt, RegenD, Interlock, RapidAkt, StrikerS, StrikerEff, PistyProc, PistyEff, BreachS, BreachEff, DillR, TrisS, TrisEff,
            FatalS, FatalEquip, FatalP, FuriousEff, FuriousGrab, EndEff, LibSS, TwinR, SynchR, Anzahl
        }
        private enum T : byte { LoaderS, StrikerS, BreachS, TrisS, FatalS, AbsS, NoctoD, NoctoH, DillR, RegenD, FatalP, SynchR, TwinR }

        internal const byte FNegiert = 1, FEnde = 2, FBann = 4, FKeinAngriff = 8;
        private const byte KeinHost = 255;

        // Aufbau des Puffers: Hand, Deck/Extra, Friedhof, zerstört, verbannt, dann Felder fester Länge
        private const int OH = 0, OD = NK, OG = 2 * NK, OZ = 3 * NK, OV = 4 * NK, OM = 5 * NK, MaxM = 7, OMF = OM + MaxM, OMP = OMF + MaxM, OS = OMP + MaxM, MaxS = 5, OSH = OS + MaxS,
            OP = OSH + MaxS, MaxP = 8, OQ = OP + MaxP, OC = OQ + MaxP, CM = OC, CS = OC + 1, CP = OC + 2, CQ = OC + 3, CNS = OC + 4, CGez = OC + 5,
            CGegM = OC + 6, CGegZ = OC + 7, CFeld = OC + 8, CLock = OC + 9, Laenge = OC + 10;

        internal sealed class Z
        {
            public byte[] B = new byte[Laenge];
            public ulong U;
            public Z Vater; public Schritt Schritt; public int Gruppe;
            public Z Kopie(Schritt s)
            {
                var z = new Z { U = U, Vater = this, Schritt = s, Gruppe = Gruppe };
                Buffer.BlockCopy(B, 0, z.B, 0, Laenge);
                return z;
            }
            public ulong Hash()
            {
                ulong h = 14695981039346656037UL ^ U;
                foreach (byte x in B) { h ^= x; h *= 1099511628211UL; }
                return h;
            }
        }

        // --- Eigenschaften der Karten -------------------------------------------------------------
        private static readonly K[] MainMonster = { K.Barrage, K.Detonator, K.Loader, K.Tracer, K.Absorouter, K.Auto, K.Magnamhut, K.Magna, K.Nocto, K.Caliber, K.Recharger, K.Synchron, K.Shel, K.Silver };
        private static readonly K[] Rokkets = { K.Tracer, K.Loader, K.Barrage, K.Silver, K.Magna, K.Shel, K.Auto, K.Recharger, K.Caliber, K.Detonator, K.Synchron };
        internal static bool IstLink(K k) => k >= K.Striker && k <= K.Shroud;
        private static bool IstExtra(K k) => k >= K.Striker && k <= K.Furious;
        private static bool IstMainMonster(K k) => k <= K.Silver;
        private static bool IstRokket(K k) => IstMainMonster(k) && k != K.Absorouter && k != K.Magnamhut && k != K.Nocto;
        private static bool IstDrache(K k) => (IstMainMonster(k) || IstExtra(k)) && k != K.Zero;
        private static bool IstDunkelDrache(K k) => IstDrache(k) && k != K.Seal;
        private static bool IstSchwimmer(K k) => k == K.Silver || k == K.Magna || k == K.Shel || k == K.Auto;
        private static int Stufe(K k)
        {
            switch (k)
            {
                case K.Barrage: return 5;
                case K.Detonator: case K.Absorouter: case K.Nocto: return 7;
                case K.Magnamhut: return 6;
                case K.Auto: return 3;
                case K.Synchron: return 1;
                case K.Shel: return 2;
                case K.Fatal: case K.Furious: return 8;
                default: return IstMainMonster(k) ? 4 : 0;
            }
        }
        internal static int LinkWert(K k)
        {
            switch (k)
            {
                case K.Striker: case K.Pisty: return 1;
                case K.Twin: case K.Breach: case K.Dilling: case K.Seal: return 2;
                case K.Tris: case K.Code: return 3;
                case K.Sword: case K.Liberator: case K.Zero: return 4;
                case K.End: case K.Shroud: return 5;
                default: return 0;
            }
        }

        // Pfeile: ↖ 1, ↑ 2, ↗ 4, ← 8, → 16, ↙ 32, ↓ 64, ↘ 128
        private const int PUL = 1, PU = 2, PUR = 4, PL = 8, PR = 16, PDL = 32, PD = 64, PDR = 128;
        private static int Pfeile(K k)
        {
            switch (k)
            {
                case K.Striker: return PL;
                case K.Pisty: return PR;
                case K.Twin: return PR | PD;
                case K.Breach: return PDL | PD;
                case K.Dilling: return PU | PD;
                case K.Seal: return PDL | PDR;
                case K.Tris: return PL | PDL | PD;
                case K.Code: return PU | PDL | PDR;
                case K.Sword: return PU | PL | PDL | PD;
                case K.Liberator: return PL | PR | PDL | PDR;
                case K.Zero: return PUL | PUR | PDL | PDR;
                case K.End: return PU | PL | PR | PDL | PDR;
                case K.Shroud: return PL | PR | PDL | PD | PDR;
                default: return 0;
            }
        }
        /// <summary>Zonen, auf die ein Link aus Zone pos zeigt (Bit 0–4 Main, 5–6 Extra-Monsterzonen).</summary>
        internal static int Zeigt(K k, int pos)
        {
            int p = Pfeile(k), m = 0;
            if (pos <= 4)
            {
                if ((p & PL) != 0 && pos > 0) m |= 1 << (pos - 1);
                if ((p & PR) != 0 && pos < 4) m |= 1 << (pos + 1);
                if ((p & PU) != 0) { if (pos == 1) m |= 1 << 5; if (pos == 3) m |= 1 << 6; }
                if ((p & PUL) != 0) { if (pos == 2) m |= 1 << 5; if (pos == 4) m |= 1 << 6; }
                if ((p & PUR) != 0) { if (pos == 0) m |= 1 << 5; if (pos == 2) m |= 1 << 6; }
            }
            else
            {
                int mitte = pos == 5 ? 1 : 3;
                if ((p & PDL) != 0) m |= 1 << (mitte - 1);
                if ((p & PD) != 0) m |= 1 << mitte;
                if ((p & PDR) != 0) m |= 1 << (mitte + 1);
            }
            return m;
        }

        private readonly List<GegnerKarte> _gegner;
        private readonly bool _ersterZug;
        private readonly int _gegnerLP;
        private readonly HashSet<string> _verboten;
        private readonly bool _hauptphase2;
        private readonly int _emzSperre;
        private readonly List<GegnerKarte> GegnerMonster, GegnerZonen;
        private int _knoten;

        internal Planer(List<GegnerKarte> gegner, bool ersterZug, int gegnerLP, HashSet<string> verboten, bool hauptphase2, int emzSperre = 0)
        {
            _gegner = gegner; _ersterZug = ersterZug; _gegnerLP = gegnerLP; _verboten = verboten; _hauptphase2 = hauptphase2; _emzSperre = emzSperre;
            GegnerMonster = _gegner.Where(g => g.Monster).Take(8).ToList();
            GegnerZonen = _gegner.Where(g => !g.Monster).Take(8).ToList();
        }

        internal int Knoten => _knoten;

        internal static Z Leer() { var z = new Z(); for (int i = 0; i < MaxS; i++) z.B[OSH + i] = KeinHost; return z; }
        internal static void SetHand(Z z, K k, int n) => z.B[OH + (int)k] = (byte)n;
        internal static void SetDeck(Z z, K k, int n) => z.B[OD + (int)k] = (byte)Math.Max(0, n);
        internal static void AddGrab(Z z, K k, bool zerstoert) { z.B[OG + (int)k]++; if (zerstoert) z.B[OZ + (int)k]++; }
        internal static void AddVerbannt(Z z, K k) => z.B[OV + (int)k]++;
        internal static void AddMonster(Z z, K k, byte flags, int zone)
        {
            int i = z.B[CM]++; z.B[OM + i] = (byte)k; z.B[OMF + i] = flags; z.B[OMP + i] = (byte)zone;
        }
        internal static void AddZone(Z z, K k, int host)
        {
            int i = z.B[CS]++; z.B[OS + i] = (byte)k; z.B[OSH + i] = host < 0 ? KeinHost : (byte)host;
        }
        internal static void SetFeldzauber(Z z) => z.B[CFeld] = 1;
        internal static void NormalVerbraucht(Z z) => z.B[CNS] = 1;
        internal static void Benutzt(Z z, E e) => z.U |= 1UL << (int)e;
        internal static bool IstBenutzt(Z z, E e) => (z.U & (1UL << (int)e)) != 0;
        internal void SetGegner(Z z)
        {
            int m = 0, s = 0, bm = 0, bs = 0;
            for (int i = 0; i < _gegner.Count; i++)
            {
                if (_gegner[i].Monster) { if (m < 8) bm |= 1 << m; m++; }
                else { if (s < 8) bs |= 1 << s; s++; }
            }
            z.B[CGegM] = (byte)bm; z.B[CGegZ] = (byte)bs;
        }

        /// <summary>Vergleichswert für „stimmt das Spiel noch mit dem Plan überein“: Hand, Friedhof, Feld (ohne Deck und Zonen).</summary>
        internal static string Signatur(Z z)
        {
            var t = new System.Text.StringBuilder();
            for (int k = 0; k < NK; k++) if (z.B[OH + k] > 0) t.Append('H').Append((K)k).Append(z.B[OH + k]);
            t.Append('|');
            for (int k = 0; k < NK; k++) if (z.B[OG + k] > 0) t.Append('G').Append((K)k).Append(z.B[OG + k]);
            t.Append('|');
            var m = new List<string>();
            for (int i = 0; i < z.B[CM]; i++) m.Add(((K)z.B[OM + i]).ToString());
            m.Sort(StringComparer.Ordinal);
            t.Append(string.Join(",", m)).Append('|');
            var zo = new List<string>();
            for (int i = 0; i < z.B[CS]; i++) zo.Add(((K)z.B[OS + i]).ToString());
            zo.Sort(StringComparer.Ordinal);
            t.Append(string.Join(",", zo)).Append(z.B[CFeld] != 0 ? "|Boot" : "");
            return t.ToString();
        }

        // =====================================================================================
        // Grundoperationen
        // =====================================================================================

        private static bool Frei(Z z, E e) => (z.U & (1UL << (int)e)) == 0;
        private static void Nutze(Z z, E e) => z.U |= 1UL << (int)e;
        private static int Belegt(Z z)
        {
            int m = 0;
            for (int i = 0; i < z.B[CM]; i++) m |= 1 << z.B[OMP + i];
            return m;
        }
        private static int Verlinkt(Z z, int ohne = 0)
        {
            int m = 0;
            for (int i = 0; i < z.B[CM]; i++)
                if ((ohne & (1 << i)) == 0 && IstLink((K)z.B[OM + i])) m |= Zeigt((K)z.B[OM + i], z.B[OMP + i]);
            return m;
        }
        /// <summary>Zone für ein Monster aus Hand, Deck oder Friedhof. Gleiche Regel im Executor (OnSelectPlace), sonst stimmen die Zonen nicht.</summary>
        internal static int ZoneMain(int frei, int verlinkt)
        {
            int f = frei & 0x1f;
            if (f == 0) return -1;
            int v = f & verlinkt;
            int w = v != 0 ? v : f;
            for (int i = 0; i < 5; i++) if ((w & (1 << i)) != 0) return i;
            return -1;
        }
        private static int FreiMain(Z z) => ~Belegt(z) & 0x1f;
        private static bool Platz(Z z) => FreiMain(z) != 0;
        private static bool STFrei(Z z) => z.B[CS] < 5;

        private static byte RausAusFeld(Z z, int i, out int pos)
        {
            byte fl = z.B[OMF + i];
            pos = z.B[OMP + i];
            int last = --z.B[CM];
            for (int j = i; j < last; j++) { z.B[OM + j] = z.B[OM + j + 1]; z.B[OMF + j] = z.B[OMF + j + 1]; z.B[OMP + j] = z.B[OMP + j + 1]; }
            return fl;
        }
        private static void ZoneRaus(Z z, int i)
        {
            int last = --z.B[CS];
            for (int j = i; j < last; j++) { z.B[OS + j] = z.B[OS + j + 1]; z.B[OSH + j] = z.B[OSH + j + 1]; }
            z.B[OSH + last] = KeinHost;
        }
        private static void Neu(Z z, T t)
        {
            for (int i = 0; i < z.B[CP]; i++) if (z.B[OP + i] == (byte)t) return;
            for (int i = 0; i < z.B[CQ]; i++) if (z.B[OQ + i] == (byte)t) return;
            if (z.B[CQ] < MaxP) z.B[OQ + z.B[CQ]++] = (byte)t;
        }
        private static void Schalten(Z z)
        {
            if (z.B[CP] == 0 && z.B[CQ] > 0)
            {
                for (int i = 0; i < z.B[CQ]; i++) z.B[OP + i] = z.B[OQ + i];
                z.B[CP] = z.B[CQ]; z.B[CQ] = 0;
                z.Gruppe++;
            }
        }

        private static void ZumFriedhof(Z z, K k, bool zerstoert, bool material = false)
        {
            if (IstExtra(k) && !IstLink(k) && false) return;
            z.B[OG + (int)k]++;
            if (zerstoert) z.B[OZ + (int)k]++;
            if (k == K.Absorouter && Frei(z, E.AbsS)) Neu(z, T.AbsS);
            if (material && k == K.Nocto && Frei(z, E.NoctoD)) Neu(z, T.NoctoD);
            if (material && k == K.Fatal && Frei(z, E.FatalP)) Neu(z, T.FatalP);
        }
        private static void GrabRaus(Z z, K k)
        {
            z.B[OG + (int)k]--;
            if (z.B[OZ + (int)k] > z.B[OG + (int)k]) z.B[OZ + (int)k] = z.B[OG + (int)k];
        }

        /// <summary>Monster verlässt das Feld. Ausrüstungen auf ihm gehen mit (Borrel Regenerator: bei Zerstörung ziehen).</summary>
        private static void VomFeld(Z z, int i, bool zerstoert, bool material)
        {
            K k = (K)z.B[OM + i];
            byte fl = RausAusFeld(z, i, out int pos);
            if ((fl & FBann) != 0) z.B[OV + (int)k]++;
            else ZumFriedhof(z, k, zerstoert, material);
            for (int s = z.B[CS] - 1; s >= 0; s--)
            {
                if (z.B[OSH + s] != pos) continue;
                K e = (K)z.B[OS + s];
                ZoneRaus(z, s);
                z.B[OG + (int)e]++;
                if (e == K.Regen && zerstoert && Frei(z, E.RegenD)) Neu(z, T.RegenD);
            }
        }

        private static void BeiBeschwoerung(Z z, K k)
        {
            if (k == K.Loader && Frei(z, E.LoaderS)) Neu(z, T.LoaderS);
            if (IstDunkelDrache(k) && z.B[OH + (int)K.Nocto] > 0 && Frei(z, E.NoctoH)) Neu(z, T.NoctoH);
            if (IstRokket(k) && z.B[OG + (int)K.Dilling] > 0 && Frei(z, E.DillR)) Neu(z, T.DillR);
        }

        /// <summary>Monster aus Hand, Deck, Friedhof oder Verbannung aufs Feld. Liefert die Zone oder -1.</summary>
        private static int Rein(Z z, K k, byte flags = 0, int zone = -1)
        {
            if (zone < 0) zone = ZoneMain(FreiMain(z), Verlinkt(z));
            if (zone < 0) return -1;
            int i = z.B[CM]++;
            z.B[OM + i] = (byte)k; z.B[OMF + i] = flags; z.B[OMP + i] = (byte)zone;
            BeiBeschwoerung(z, k);
            return zone;
        }

        /// <summary>Eigene Karte zerstören. Ziel: Monster-Index oder Zauber-Index, -1 = Feldzauber.</summary>
        private static void Zerstoere(Z z, (bool monster, int i) t)
        {
            if (t.monster) VomFeld(z, t.i, true, false);
            else if (t.i < 0) { z.B[CFeld] = 0; z.B[OG + (int)K.Boot]++; }
            else
            {
                K k = (K)z.B[OS + t.i];
                int host = z.B[OSH + t.i];
                ZoneRaus(z, t.i);
                z.B[OG + (int)k]++;
                if (k == K.Regen && host != KeinHost)
                    for (int m = 0; m < z.B[CM]; m++)
                        if (z.B[OMP + m] == host) { K b = (K)z.B[OM + m]; RausAusFeld(z, m, out _); z.B[OV + (int)b]++; break; }
            }
        }

        private static void Ziele(Z z, List<(bool monster, int i)> liste, bool nurMonster = false)
        {
            liste.Clear();
            for (int i = 0; i < z.B[CM]; i++)
            {
                K k = (K)z.B[OM + i];
                if (k == K.End || k == K.Shroud || k == K.Liberator) continue;
                bool dop = false;
                for (int j = 0; j < i; j++) if (z.B[OM + j] == z.B[OM + i] && z.B[OMF + j] == z.B[OMF + i]) dop = true;
                if (!dop) liste.Add((true, i));
            }
            if (nurMonster) return;
            if (z.B[CFeld] != 0) liste.Add((false, -1));
            for (int i = 0; i < z.B[CS]; i++) if (z.B[OSH + i] == KeinHost && z.B[OS + i] != (byte)K.Anderes) liste.Add((false, i));
        }
        private static Wahl WahlFeld(Z z, (bool monster, int i) t) => t.monster
            ? new Wahl((K)z.B[OM + t.i], O.Monster, -1, (byte)(z.B[OMF + t.i] & (FNegiert | FEnde)))
            : t.i < 0 ? new Wahl(K.Boot, O.FeldZauber) : new Wahl((K)z.B[OS + t.i], O.Zone);

        // =====================================================================================
        // Züge erzeugen
        // =====================================================================================

        private readonly List<(bool monster, int i)> _z = new List<(bool, int)>();

        private void Kind(List<Z> aus, Z z) { Schalten(z); aus.Add(z); }
        private bool Erlaubt(Schritt s) => _verboten == null || _verboten.Count == 0 || !_verboten.Contains(s.Art + " " + s.Karte + " " + s.Ort);
        private Schritt Akt(K k, O o, params Wahl[] w) => new Schritt { Art = Art.Aktivieren, Karte = k, Ort = o, Wahlen = w };
        private static void Dunkel(Z z) => z.B[CLock] |= 1;

        internal void Erweitere(Z v, List<Z> aus)
        {
            if (v.B[CP] > 0) { Ausloeser(v, aus); return; }

            // Normalbeschwörung
            if (v.B[CNS] == 0 && Platz(v))
            {
                foreach (K k in new[] { K.Loader, K.Synchron, K.Tracer, K.Caliber, K.Silver, K.Magna, K.Recharger, K.Shel, K.Auto })
                {
                    if (v.B[OH + (int)k] == 0) continue;
                    if (k == K.Synchron && !SynchronZiel(v)) continue;
                    var s = new Schritt { Art = Art.Normal, Karte = k, Ort = O.Hand };
                    if (!Erlaubt(s)) continue;
                    var z = v.Kopie(s);
                    z.B[OH + (int)k]--; z.B[CNS] = 1;
                    { int i = z.B[CM]++; z.B[OM + i] = (byte)k; z.B[OMF + i] = 0; z.B[OMP + i] = (byte)ZoneMain(FreiMain(v), Verlinkt(v)); }
                    if (k == K.Loader && Frei(z, E.LoaderS)) Neu(z, T.LoaderS);
                    if (k == K.Synchron && Frei(z, E.SynchR)) Neu(z, T.SynchR);
                    Kind(aus, z);
                }
            }

            // Beschwörungen von der Hand ohne Kette
            if (v.B[OH + (int)K.Absorouter] > 0 && Frei(v, E.AbsProc) && Platz(v) && HatRokket(v)) Proc(v, aus, K.Absorouter, E.AbsProc);
            if (v.B[OH + (int)K.Detonator] > 0 && Frei(v, E.DetProc) && Platz(v) && HatDunkelLink(v)) Proc(v, aus, K.Detonator, E.DetProc);
            if (v.B[OH + (int)K.Caliber] > 0 && Frei(v, E.CaliberProc) && (FreiMain(v) & Verlinkt(v)) != 0) Proc(v, aus, K.Caliber, E.CaliberProc);

            // Handeffekte
            if (v.B[OH + (int)K.Barrage] > 0 && Frei(v, E.BarrageHand) && Platz(v)) BarrageHand(v, aus);
            if (v.B[OH + (int)K.Magnamhut] > 0 && Frei(v, E.MagnamHand) && Platz(v)) Magnamhut(v, aus);

            // Zauber von der Hand
            if (v.B[OH + (int)K.Boot] > 0 && v.B[CFeld] == 0)
            {
                var s = Akt(K.Boot, O.Hand);
                if (Erlaubt(s)) { var z = v.Kopie(s); z.B[OH + (int)K.Boot]--; z.B[CFeld] = 1; Kind(aus, z); }
            }
            if (v.B[CFeld] != 0 && Frei(v, E.BootEff)) BootEffekt(v, aus);
            if (STFrei(v))
            {
                if (v.B[OH + (int)K.Launch] > 0 && Platz(v)) QuickLaunch(v, aus);
                if (v.B[OH + (int)K.Triple] > 0 && Platz(v) && Frei(v, E.TripleA)) TripleA(v, aus);
                if (v.B[OH + (int)K.Triple] > 0 && Frei(v, E.TripleB)) TripleB(v, aus);
                if (v.B[OH + (int)K.Regen] > 0 && Frei(v, E.RegenAkt) && Platz(v)) RegenAkt(v, aus);
                if (v.B[OH + (int)K.Interlock] > 0 && Frei(v, E.Interlock) && Platz(v)) Interlock(v, aus);
                if (v.B[OH + (int)K.Rapid] > 0 && Frei(v, E.RapidAkt)) RapidTrigger(v, aus);
            }

            // Feldeffekte
            for (int i = 0; i < v.B[CM]; i++)
            {
                K k = (K)v.B[OM + i]; byte f = v.B[OMF + i];
                if ((f & FNegiert) != 0) continue;
                if (k == K.Tracer && Frei(v, E.TracerEff)) Tracer(v, aus);
                if (k == K.Detonator && Frei(v, E.DetEff)) Detonator(v, aus);
                if (k == K.Fatal && Frei(v, E.FatalEquip)) FatalEquip(v, aus);
                if (k == K.Striker && Frei(v, E.StrikerEff)) Striker(v, aus);
                if (k == K.Tris && Frei(v, E.TrisEff)) Tris(v, aus);
                if (k == K.Breach && Frei(v, E.BreachEff)) Breach(v, aus);
                if (k == K.End && Frei(v, E.EndEff)) End(v, aus);
                if (k == K.Pisty && Frei(v, E.PistyEff)) Pisty(v, aus);
                if (k == K.Caliber && Frei(v, E.CaliberTrib)) CaliberTrib(v, aus, i);
                if (k == K.Furious && Frei(v, E.FuriousEff)) FuriousEff(v, aus);
            }
            // Friedhof
            if (v.B[OG + (int)K.Furious] > 0 && Frei(v, E.FuriousGrab) && Platz(v)) FuriousGrab(v, aus);
            if (v.B[OG + (int)K.Liberator] > 0 && Frei(v, E.LibSS)) LiberatorGrab(v, aus);

            // Links
            LinkZuege(v, aus);
        }

        private static bool HatLink5(Z z)
        {
            for (int i = 0; i < z.B[CM]; i++) { K k = (K)z.B[OM + i]; if (k == K.End || k == K.Shroud) return true; }
            return false;
        }
        private static bool HatRokket(Z z)
        {
            for (int i = 0; i < z.B[CM]; i++) if (IstRokket((K)z.B[OM + i])) return true;
            return false;
        }
        private static bool HatDunkelLink(Z z)
        {
            for (int i = 0; i < z.B[CM]; i++) { K k = (K)z.B[OM + i]; if (IstLink(k) && IstDunkelDrache(k)) return true; }
            return false;
        }
        private static bool SynchronZiel(Z z)
        {
            foreach (K k in MainMonster) if (Stufe(k) >= 5 && z.B[OG + (int)k] > 0) return true;
            return false;
        }

        private void Proc(Z v, List<Z> aus, K k, E e)
        {
            var s = new Schritt { Art = Art.Proc, Karte = k, Ort = O.Hand };
            if (!Erlaubt(s)) return;
            var z = v.Kopie(s);
            Nutze(z, e); z.B[OH + (int)k]--;
            if (k == K.Caliber) Rein(z, k, 0, ZoneMain(FreiMain(z) & Verlinkt(z), 0));
            else Rein(z, k);
            Kind(aus, z);
        }

        private void BarrageHand(Z v, List<Z> aus)
        {
            foreach (K k in new[] { K.Absorouter, K.Silver, K.Tracer, K.Loader, K.Shel, K.Magna, K.Auto, K.Nocto, K.Detonator, K.Recharger, K.Caliber, K.Synchron })
            {
                if (v.B[OD + (int)k] == 0) continue;
                var s = Akt(K.Barrage, O.Hand, new Wahl(k, O.Deck));
                if (!Erlaubt(s)) continue;
                var z = v.Kopie(s);
                Nutze(z, E.BarrageHand); Dunkel(z);
                z.B[OD + (int)k]--; z.B[OH + (int)K.Barrage]--;
                ZumFriedhof(z, k, false);
                Rein(z, K.Barrage);
                Kind(aus, z);
            }
        }

        private void Magnamhut(Z v, List<Z> aus)
        {
            foreach (K k in MainMonster)
            {
                if (v.B[OG + (int)k] == 0) continue;
                var s = Akt(K.Magnamhut, O.Hand, new Wahl(k, O.Grab));
                if (!Erlaubt(s)) continue;
                var z = v.Kopie(s);
                Nutze(z, E.MagnamHand);
                z.B[OH + (int)K.Magnamhut]--;
                GrabRaus(z, k); z.B[OV + (int)k]++;
                Rein(z, K.Magnamhut);
                Kind(aus, z);
            }
        }

        private void BootEffekt(Z v, List<Z> aus)
        {
            // Hand: bis zu 2 verschiedene Rokkets
            var hand = Rokkets.Where(k => v.B[OH + (int)k] > 0).ToList();
            int frei = 5 - System.Linq.Enumerable.Range(0, 5).Count(i => (Belegt(v) & (1 << i)) != 0);
            for (int a = 0; a < hand.Count && frei > 0; a++)
            {
                BootHand(v, aus, new List<K> { hand[a] });
                if (frei > 1) for (int b = a + 1; b < hand.Count; b++) BootHand(v, aus, new List<K> { hand[a], hand[b] });
            }
            // Friedhof: wenn der Gegner mehr Monster hat, bis zur Differenz
            int gm = GegnerZahl(v), eigene = v.B[CM];
            if (gm > eigene && frei > 0)
            {
                var grab = Rokkets.Where(k => v.B[OG + (int)k] > 0).ToList();
                int n = Math.Min(Math.Min(gm - eigene, frei), grab.Count);
                if (n > 0)
                {
                    var wahl = grab.Take(n).ToList();
                    var s = Akt(K.Boot, O.FeldZauber, wahl.Select(k => new Wahl(k, O.Grab)).ToArray());
                    s.Option = 1;
                    if (Erlaubt(s))
                    {
                        var z = v.Kopie(s);
                        Nutze(z, E.BootEff);
                        foreach (K k in wahl) { GrabRaus(z, k); Rein(z, k); }
                        Kind(aus, z);
                    }
                }
            }
        }
        private void BootHand(Z v, List<Z> aus, List<K> wahl)
        {
            var s = Akt(K.Boot, O.FeldZauber, wahl.Select(k => new Wahl(k, O.Hand)).ToArray());
            s.Option = 0;
            if (!Erlaubt(s)) return;
            var z = v.Kopie(s);
            Nutze(z, E.BootEff);
            foreach (K k in wahl) { z.B[OH + (int)k]--; Rein(z, k); }
            Kind(aus, z);
        }

        private void QuickLaunch(Z v, List<Z> aus)
        {
            foreach (K k in Rokkets)
            {
                if (v.B[OD + (int)k] == 0) continue;
                var s = Akt(K.Launch, O.Hand, new Wahl(k, O.Deck));
                if (!Erlaubt(s)) continue;
                var z = v.Kopie(s);
                z.B[OH + (int)K.Launch]--; z.B[OG + (int)K.Launch]++;
                z.B[OD + (int)k]--;
                Rein(z, k, (byte)(FEnde | FKeinAngriff));
                Kind(aus, z);
            }
        }

        private void TripleA(Z v, List<Z> aus)
        {
            // 1 Drache aus dem Friedhof zurück, dann 1 Rokket mit anderem Namen. Billigste zuerst, sonst wird die Suche zu breit.
            var grab = MainMonster.Where(k => v.B[OG + (int)k] > 0).OrderBy(k => IstRokket(k) ? 0 : 1).Take(3).ToList();
            foreach (K weg in grab)
                foreach (K k in Rokkets)
                {
                    if (k == weg || v.B[OD + (int)k] == 0) continue;
                    var s = Akt(K.Triple, O.Hand, new Wahl(weg, O.Grab), new Wahl(k, O.Deck));
                    s.Option = 0;
                    if (!Erlaubt(s)) continue;
                    var z = v.Kopie(s);
                    Nutze(z, E.TripleA);
                    z.B[OH + (int)K.Triple]--; z.B[OG + (int)K.Triple]++;
                    GrabRaus(z, weg); z.B[OD + (int)weg]++;
                    z.B[OD + (int)k]--;
                    Rein(z, k);
                    Kind(aus, z);
                }
        }
        private void TripleB(Z v, List<Z> aus)
        {
            if (v.B[OG + (int)K.Boot] == 0) return;
            var grab = MainMonster.Where(k => v.B[OG + (int)k] > 0).SelectMany(k => Enumerable.Repeat(k, v.B[OG + (int)k])).Take(2).ToList();
            if (grab.Count < 2) return;
            var s = Akt(K.Triple, O.Hand, new Wahl(grab[0], O.Grab), new Wahl(grab[1], O.Grab), new Wahl(K.Boot, O.Grab));
            s.Option = 1;
            if (!Erlaubt(s)) return;
            var z = v.Kopie(s);
            Nutze(z, E.TripleB);
            z.B[OH + (int)K.Triple]--; z.B[OG + (int)K.Triple]++;
            foreach (K k in grab) { GrabRaus(z, k); z.B[OD + (int)k]++; }
            z.B[OG + (int)K.Boot]--; z.B[OH + (int)K.Boot]++;
            Kind(aus, z);
        }

        private void RegenAkt(Z v, List<Z> aus)
        {
            foreach (K k in Rokkets)
            {
                if (v.B[OG + (int)k] == 0) continue;
                var s = Akt(K.Regen, O.Hand, new Wahl(k, O.Grab));
                if (!Erlaubt(s)) continue;
                var z = v.Kopie(s);
                Nutze(z, E.RegenAkt);
                z.B[OH + (int)K.Regen]--;
                GrabRaus(z, k);
                int pos = Rein(z, k);
                AddZone(z, K.Regen, pos);
                Kind(aus, z);
            }
        }

        private void Interlock(Z v, List<Z> aus)
        {
            // Fatalflare: 1 DUNKEL-Link + 1 DUNKEL-Monster, darunter ein Rokket
            if (v.B[OD + (int)K.Fatal] > 0)
            {
                foreach (K link in new[] { K.Striker, K.Pisty, K.Dilling, K.Breach, K.Twin, K.Tris, K.Code })
                {
                    if (v.B[OG + (int)link] == 0) continue;
                    foreach (K r in Rokkets)
                    {
                        if (v.B[OG + (int)r] == 0) continue;
                        var s = Akt(K.Interlock, O.Hand, new Wahl(K.Fatal, O.Extra), new Wahl(link, O.Grab), new Wahl(r, O.Grab));
                        s.Option = 0;
                        if (!Erlaubt(s)) continue;
                        var z = v.Kopie(s);
                        Nutze(z, E.Interlock);
                        z.B[OH + (int)K.Interlock]--; z.B[OG + (int)K.Interlock]++;
                        GrabRaus(z, link); z.B[OV + (int)link]++;
                        GrabRaus(z, r); z.B[OV + (int)r]++;
                        z.B[OD + (int)K.Fatal]--;
                        Rein(z, K.Fatal);
                        if (Frei(z, E.FatalS)) Neu(z, T.FatalS);
                        Kind(aus, z);
                    }
                }
            }
            // Furious: 2 DUNKEL-Drachen, darunter ein Rokket
            if (v.B[OD + (int)K.Furious] > 0)
            {
                var r = Rokkets.Where(k => v.B[OG + (int)k] > 0).Take(3).ToList();
                foreach (K a in r)
                    foreach (K b in MainMonster.Where(k => v.B[OG + (int)k] > (k == a ? 1 : 0)))
                    {
                        if (b < a && IstRokket(b)) continue;
                        var s = Akt(K.Interlock, O.Hand, new Wahl(K.Furious, O.Extra), new Wahl(a, O.Grab), new Wahl(b, O.Grab));
                        s.Option = 0;
                        if (!Erlaubt(s)) continue;
                        var z = v.Kopie(s);
                        Nutze(z, E.Interlock);
                        z.B[OH + (int)K.Interlock]--; z.B[OG + (int)K.Interlock]++;
                        GrabRaus(z, a); z.B[OV + (int)a]++;
                        GrabRaus(z, b); z.B[OV + (int)b]++;
                        z.B[OD + (int)K.Furious]--;
                        Rein(z, K.Furious);
                        Kind(aus, z);
                    }
            }
        }

        private void RapidTrigger(Z v, List<Z> aus)
        {
            if (v.B[OD + (int)K.Furious] == 0) return;
            for (int a = 0; a < v.B[CM]; a++)
            {
                K ka = (K)v.B[OM + a];
                if (!IstMainMonster(ka)) continue;
                for (int b = a + 1; b < v.B[CM]; b++)
                {
                    K kb = (K)v.B[OM + b];
                    if (!IstMainMonster(kb)) continue;
                    var s = Akt(K.Rapid, O.Hand, new Wahl(K.Furious, O.Extra), new Wahl(ka, O.Monster, -1, (byte)(v.B[OMF + a] & (FNegiert | FEnde))), new Wahl(kb, O.Monster, -1, (byte)(v.B[OMF + b] & (FNegiert | FEnde))));
                    if (!Erlaubt(s)) continue;
                    var z = v.Kopie(s);
                    Nutze(z, E.RapidAkt);
                    z.B[OH + (int)K.Rapid]--; z.B[OG + (int)K.Rapid]++;
                    VomFeld(z, b, true, false); VomFeld(z, a, true, false);
                    z.B[OD + (int)K.Furious]--;
                    Rein(z, K.Furious);
                    Kind(aus, z);
                }
            }
        }

        private void Tracer(Z v, List<Z> aus)
        {
            Ziele(v, _z);
            var ziele = _z.ToList();
            foreach (var t in ziele)
            {
                foreach (K k in new[] { K.Loader, K.Barrage, K.Silver, K.Magna, K.Shel, K.Auto, K.Detonator, K.Caliber, K.Recharger, K.Synchron })
                {
                    if (v.B[OD + (int)k] == 0) continue;
                    var s = Akt(K.Tracer, O.Monster, WahlFeld(v, t), new Wahl(k, O.Deck));
                    if (!Erlaubt(s)) continue;
                    var z = v.Kopie(s);
                    Nutze(z, E.TracerEff); Dunkel(z);
                    Zerstoere(z, t);
                    z.B[OD + (int)k]--;
                    if (Rein(z, k) < 0) continue;
                    Kind(aus, z);
                }
            }
        }

        private void Detonator(Z v, List<Z> aus)
        {
            for (int i = 0; i < v.B[CS]; i++)
            {
                K k = (K)v.B[OS + i];
                if (!IstDunkelDrache(k) || !Platz(v)) continue;
                var s = Akt(K.Detonator, O.Monster, new Wahl(k, O.Zone));
                if (!Erlaubt(s)) continue;
                var z = v.Kopie(s);
                Nutze(z, E.DetEff); Dunkel(z);
                ZoneRaus(z, i);
                Rein(z, k, (byte)(FNegiert | FKeinAngriff));
                Kind(aus, z);
            }
        }

        private void FatalEquip(Z v, List<Z> aus)
        {
            if (!STFrei(v)) return;
            foreach (K link in new[] { K.Tris, K.Dilling, K.Breach, K.Striker, K.Pisty, K.Twin, K.Code, K.Sword, K.Liberator, K.End, K.Shroud })
            {
                bool grab = v.B[OG + (int)link] > 0, ver = v.B[OV + (int)link] > 0;
                if (!grab && !ver) continue;
                O ort = grab ? O.Grab : O.Verbannt;
                for (int h = 0; h < v.B[CM]; h++)
                {
                    K host = (K)v.B[OM + h];
                    // Ausrüsten nur als Zwischenschritt zu Detonator oder als 500 ATK auf das Boss-Monster
                    if (host != K.Detonator && host != K.Fatal && host != K.End && host != K.Shroud) continue;
                    var s = Akt(K.Fatal, O.Monster, new Wahl(host, O.Monster), new Wahl(link, ort));
                    if (!Erlaubt(s)) continue;
                    var z = v.Kopie(s);
                    Nutze(z, E.FatalEquip);
                    if (grab) GrabRaus(z, link); else z.B[OV + (int)link]--;
                    AddZone(z, link, z.B[OMP + h]);
                    Kind(aus, z);
                }
            }
        }

        private void Striker(Z v, List<Z> aus)
        {
            Ziele(v, _z, true);
            var ziele = _z.ToList();
            foreach (var t in ziele)
                foreach (K r in Rokkets)
                {
                    if (v.B[OG + (int)r] == 0) continue;
                    var s = Akt(K.Striker, O.Monster, WahlFeld(v, t), new Wahl(r, O.Grab));
                    if (!Erlaubt(s)) continue;
                    var z = v.Kopie(s);
                    Nutze(z, E.StrikerEff);
                    Zerstoere(z, t);
                    GrabRaus(z, r); z.B[OH + (int)r]++;
                    Kind(aus, z);
                }
        }

        private void Tris(Z v, List<Z> aus)
        {
            Ziele(v, _z);
            var ziele = _z.ToList();
            foreach (var t in ziele)
                foreach (K r in MainMonster)
                {
                    if (v.B[OG + (int)r] == 0) continue;
                    var s = Akt(K.Tris, O.Monster, WahlFeld(v, t), new Wahl(r, O.Grab));
                    if (!Erlaubt(s)) continue;
                    var z = v.Kopie(s);
                    Nutze(z, E.TrisEff);
                    Zerstoere(z, t);
                    GrabRaus(z, r); z.B[OH + (int)r]++;
                    Kind(aus, z);
                }
        }

        /// <summary>Breachborrel zielt auf ein eigenes Monster, das als Antwort selbst zerstört: dann Rokket aus dem Deck (Ketos Hauptlinie).</summary>
        private void Breach(Z v, List<Z> aus)
        {
            for (int i = 0; i < v.B[CM]; i++)
            {
                K x = (K)v.B[OM + i];
                if ((v.B[OMF + i] & FNegiert) != 0) continue;
                E eff;
                switch (x)
                {
                    case K.Silver: eff = E.SilverZ; break;
                    case K.Barrage: eff = E.BarrageZ; break;
                    case K.Magna: eff = E.MagnaZ; break;
                    case K.Shel: eff = E.ShelZ; break;
                    case K.Auto: eff = E.AutoZ; break;
                    default: continue;
                }
                if (!Frei(v, eff)) continue;
                bool dop = false;
                for (int j = 0; j < i; j++) if (v.B[OM + j] == (byte)x && (v.B[OMF + j] & FNegiert) == 0) dop = true;
                if (dop) continue;
                int gegnerZiel = -1;
                if (x == K.Magna || x == K.Shel)
                {
                    gegnerZiel = BesterGegnerMonster(v, x == K.Shel);
                    if (gegnerZiel < 0) continue;
                }
                if (x == K.Auto)
                {
                    gegnerZiel = BesteGegnerZone(v);
                    if (gegnerZiel < 0) continue;
                }
                foreach (K nach in new[] { K.Tracer, K.Detonator, K.Loader, K.Silver, K.Magna, K.Shel, K.Auto, K.Recharger, K.Caliber, K.Barrage })
                {
                    if (v.B[OD + (int)nach] == 0) continue;
                    if (x == K.Barrage) { BreachBarrage(v, aus, i, nach); continue; }
                    var s1 = Akt(K.Breach, O.Monster, new Wahl(x, O.Monster), new Wahl(nach, O.Deck));
                    if (!Erlaubt(s1)) continue;
                    var z1 = v.Kopie(s1);
                    z1.Gruppe++;
                    var s2 = new Schritt { Art = Art.Ausloeser, Karte = x, Ort = O.Monster, Gruppe = z1.Gruppe, Wahlen = gegnerZiel >= 0 ? new[] { GegnerWahl(gegnerZiel) } : Array.Empty<Wahl>() };
                    var z = z1.Kopie(s2);
                    Nutze(z, E.BreachEff); Nutze(z, eff);
                    VomFeld(z, i, true, false);
                    if (gegnerZiel >= 0) EntferneGegner(z, gegnerZiel);
                    z.B[OD + (int)nach]--;
                    if (Rein(z, nach) < 0) continue;
                    Kind(aus, z);
                }
            }
        }

        /// <summary>Barrage als Antwort: zerstört sich, dann je 1 Rokket aus Hand, Deck, Friedhof, Verbannung. Duell 02.11. 23:10 und 03.11. 16:48: so entstehen 3–5 Monster.</summary>
        private void BreachBarrage(Z v, List<Z> aus, int i, K breachNach)
        {
            var deck = Rokkets.Where(k => k != K.Barrage && v.B[OD + (int)k] > (k == breachNach ? 1 : 0)).Take(5).ToList();
            var grab = Rokkets.Where(k => k != K.Barrage && v.B[OG + (int)k] > 0).Take(3).Cast<K?>().ToList();
            grab.Add(null);
            K? hand = Rokkets.Where(k => k != K.Barrage && v.B[OH + (int)k] > 0).Cast<K?>().FirstOrDefault();
            K? ver = Rokkets.Where(k => k != K.Barrage && v.B[OV + (int)k] > 0).Cast<K?>().FirstOrDefault();
            foreach (K d in deck)
                foreach (K? g in grab)
                {
                    var w = new List<Wahl>();
                    w.Add(new Wahl(K.Barrage, O.Monster));
                    w.Add(new Wahl(d, O.Deck));
                    if (g != null) w.Add(new Wahl(g.Value, O.Grab));
                    if (hand != null) w.Add(new Wahl(hand.Value, O.Hand));
                    if (ver != null) w.Add(new Wahl(ver.Value, O.Verbannt));
                    w.Add(new Wahl(breachNach, O.Deck));
                    var s1 = Akt(K.Breach, O.Monster, w.ToArray());
                    if (!Erlaubt(s1)) continue;
                    var z1 = v.Kopie(s1);
                    z1.Gruppe++;
                    var z = z1.Kopie(new Schritt { Art = Art.Ausloeser, Karte = K.Barrage, Ort = O.Monster, Gruppe = z1.Gruppe });
                    Nutze(z, E.BreachEff); Nutze(z, E.BarrageZ);
                    VomFeld(z, i, true, false);
                    z.B[OD + (int)d]--; bool ok = Rein(z, d) >= 0;
                    if (g != null && ok) { GrabRaus(z, g.Value); ok = Rein(z, g.Value) >= 0; }
                    if (hand != null && ok) { z.B[OH + (int)hand.Value]--; ok = Rein(z, hand.Value) >= 0; }
                    if (ver != null && ok) { z.B[OV + (int)ver.Value]--; ok = Rein(z, ver.Value) >= 0; }
                    if (!ok) continue;
                    z.B[OD + (int)breachNach]--;
                    if (Rein(z, breachNach) < 0) continue;
                    Kind(aus, z);
                }
        }

        private void End(Z v, List<Z> aus)
        {
            if (!Platz(v)) return;
            var grab = Rokkets.Where(k => v.B[OG + (int)k] > 0).ToList();
            if (grab.Count == 0) return;
            // Ziel: bestes Gegnermonster, sonst ein eigenes nicht negiertes Effektmonster
            int gegner = BesterGegnerNegierbar(v);
            var eigene = new List<int>();
            for (int i = 0; i < v.B[CM]; i++)
            {
                K k = (K)v.B[OM + i];
                if ((v.B[OMF + i] & FNegiert) != 0) continue;
                if (k == K.End || k == K.Shroud || k == K.Liberator || k == K.Sword) continue;
                bool dop = false;
                foreach (int j in eigene) if ((K)v.B[OM + j] == k) dop = true;
                if (!dop) eigene.Add(i);
            }
            foreach (K r in grab)
            {
                if (gegner >= 0)
                {
                    var s = Akt(K.End, O.Monster, GegnerWahl(gegner), new Wahl(r, O.Grab));
                    if (Erlaubt(s))
                    {
                        var z = v.Kopie(s);
                        Nutze(z, E.EndEff);
                        GrabRaus(z, r); Rein(z, r);
                        z.B[CGez] += 0;
                        Kind(aus, z);
                    }
                }
                foreach (int i in eigene)
                {
                    var s = Akt(K.End, O.Monster, new Wahl((K)v.B[OM + i], O.Monster, -1, (byte)(v.B[OMF + i] & FEnde)), new Wahl(r, O.Grab));
                    if (!Erlaubt(s)) continue;
                    var z = v.Kopie(s);
                    Nutze(z, E.EndEff);
                    z.B[OMF + i] |= FNegiert;
                    GrabRaus(z, r); Rein(z, r);
                    Kind(aus, z);
                }
            }
        }

        private void Pisty(Z v, List<Z> aus)
        {
            // Zone, auf die 2+ Links zeigen
            int zweifach = 0, einmal = 0;
            for (int i = 0; i < v.B[CM]; i++)
            {
                K k = (K)v.B[OM + i];
                if (!IstLink(k)) continue;
                int m = Zeigt(k, v.B[OMP + i]);
                zweifach |= einmal & m;
                einmal |= m;
            }
            int zone = ZoneMain(FreiMain(v) & zweifach, 0);
            if (zone < 0) return;
            foreach (K k in Rokkets.Concat(new[] { K.Absorouter, K.Nocto, K.Striker, K.Dilling, K.Breach, K.Twin, K.Tris }))
            {
                bool grab = v.B[OG + (int)k] > 0, ver = v.B[OV + (int)k] > 0;
                if (!grab && !ver) continue;
                var s = Akt(K.Pisty, O.Monster, new Wahl(k, grab ? O.Grab : O.Verbannt));
                s.Zone = (sbyte)zone;
                if (!Erlaubt(s)) continue;
                var z = v.Kopie(s);
                Nutze(z, E.PistyEff);
                if (grab) GrabRaus(z, k); else z.B[OV + (int)k]--;
                Rein(z, k, 0, zone);
                Kind(aus, z);
            }
        }

        private void CaliberTrib(Z v, List<Z> aus, int i)
        {
            foreach (K k in MainMonster)
            {
                if (k == K.Caliber || v.B[OH + (int)k] == 0) continue;
                var s = Akt(K.Caliber, O.Monster, new Wahl(k, O.Hand));
                if (!Erlaubt(s)) continue;
                var z = v.Kopie(s);
                Nutze(z, E.CaliberTrib);
                VomFeld(z, i, false, false);
                z.B[OH + (int)k]--;
                Rein(z, k);
                Kind(aus, z);
            }
        }

        private void FuriousEff(Z v, List<Z> aus)
        {
            int g = BesteGegnerKarte(v);
            if (g < 0) return;
            Ziele(v, _z, true);
            foreach (var t in _z.ToList())
            {
                if ((K)v.B[OM + t.i] == K.Furious) continue;
                var s = Akt(K.Furious, O.Monster, WahlFeld(v, t), GegnerWahl(g));
                if (!Erlaubt(s)) continue;
                var z = v.Kopie(s);
                Nutze(z, E.FuriousEff);
                Zerstoere(z, t);
                EntferneGegner(z, g);
                Kind(aus, z);
            }
        }

        private void FuriousGrab(Z v, List<Z> aus)
        {
            foreach (K link in new[] { K.End, K.Shroud, K.Liberator, K.Sword, K.Tris, K.Breach })
            {
                if (v.B[OG + (int)link] == 0) continue;
                var s = Akt(K.Furious, O.Grab, new Wahl(link, O.Grab));
                if (!Erlaubt(s)) continue;
                var z = v.Kopie(s);
                Nutze(z, E.FuriousGrab);
                GrabRaus(z, K.Furious); z.B[OV + (int)K.Furious]++;
                GrabRaus(z, link);
                Rein(z, link, FNegiert);   // kann diesen Zug keine Effekte aktivieren (Duell 02.11. 23:04), die Dauereffekte bleiben
                Kind(aus, z);
            }
        }

        private void LiberatorGrab(Z v, List<Z> aus)
        {
            Ziele(v, _z, true);
            foreach (var t in _z.ToList())
            {
                var s = Akt(K.Liberator, O.Grab, WahlFeld(v, t));
                if (!Erlaubt(s)) continue;
                var z = v.Kopie(s);
                Nutze(z, E.LibSS);
                Zerstoere(z, t);
                GrabRaus(z, K.Liberator);
                Rein(z, K.Liberator);
                Kind(aus, z);
            }
        }

        // --- Links -----------------------------------------------------------------------------------

        private static readonly K[] LinkArten = { K.Shroud, K.End, K.Liberator, K.Sword, K.Tris, K.Code, K.Breach, K.Dilling, K.Twin, K.Striker, K.Pisty };

        private static bool RatingMoeglich(List<K> mat, int ziel)
        {
            int n = mat.Count;
            for (int m = 0; m < 1 << n; m++)
            {
                int w = 0; bool ok = true;
                for (int i = 0; i < n; i++)
                {
                    if ((m & (1 << i)) != 0) { if (!IstLink(mat[i])) ok = false; w += LinkWert(mat[i]); }
                    else w += 1;
                }
                if (ok && w == ziel) return true;
            }
            return false;
        }

        private static bool MaterialPasst(K ziel, List<K> mat)
        {
            int wert = LinkWert(ziel);
            switch (ziel)
            {
                case K.Striker: case K.Pisty:
                    return mat.Count == 1 && IstDrache(mat[0]) && Stufe(mat[0]) >= 1 && Stufe(mat[0]) <= 4;
                case K.Twin:
                    return mat.Count == 2 && mat.All(k => IstDrache(k) && Stufe(k) >= 1 && Stufe(k) <= 4);
                case K.Breach:
                    return mat.Count == 2 && mat.All(IstDunkelDrache) && mat.Any(IstRokket);
                case K.Dilling:
                    return mat.Count == 2 && mat.All(IstDunkelDrache);
                case K.Tris:
                    return mat.Count >= 2 && mat.Count <= 3 && mat.All(IstDunkelDrache) && mat.Any(IstRokket) && RatingMoeglich(mat, wert);
                case K.Code:
                    return mat.Count >= 2 && mat.Count <= 3 && RatingMoeglich(mat, wert);
                case K.Sword: case K.Liberator:
                    return mat.Count >= 3 && mat.Count <= 4 && RatingMoeglich(mat, wert);
                case K.End: case K.Shroud:
                    return mat.Count >= 3 && mat.Count <= 5 && RatingMoeglich(mat, wert);
                default: return false;
            }
        }

        private void LinkZuege(Z v, List<Z> aus)
        {
            int n = v.B[CM];
            if (n == 0) return;
            var gesehen = new HashSet<long>();
            foreach (K ziel in LinkArten)
            {
                if (v.B[OD + (int)ziel] == 0) continue;
                if (ziel == K.Liberator && !Frei(v, E.LibSS)) continue;
                if (ziel == K.Pisty && !Frei(v, E.PistyProc)) continue;
                for (int maske = 1; maske < 1 << n; maske++)
                {
                    int bits = 0;
                    for (int b = 0; b < n; b++) if ((maske & (1 << b)) != 0) bits++;
                    if (bits > 5) continue;
                    var mat = new List<K>(bits);
                    var matWahl = new List<Wahl>(bits);
                    bool sperre = false;
                    for (int b = 0; b < n; b++)
                        if ((maske & (1 << b)) != 0)
                        {
                            K m = (K)v.B[OM + b];
                            if (m == K.End || m == K.Shroud) sperre = true;
                            mat.Add(m);
                            matWahl.Add(new Wahl(m, O.Monster, -1, (byte)(v.B[OMF + b] & (FNegiert | FEnde))));
                        }
                    if (sperre || !MaterialPasst(ziel, mat)) continue;
                    // Wo darf der Link hin, wenn die Materialien weg sind?
                    int rest = 0, emzRest = 0;
                    for (int i = 0; i < n; i++)
                        if ((maske & (1 << i)) == 0)
                        {
                            rest |= 1 << v.B[OMP + i];
                            if (v.B[OMP + i] >= 5) emzRest |= 1 << v.B[OMP + i];
                        }
                    int verlinkt = Verlinkt(v, maske);
                    int erlaubt = verlinkt & ~rest & 0x7f;
                    if (emzRest == 0) erlaubt |= (1 << 5 | 1 << 6) & ~_emzSperre;
                    erlaubt &= ~_emzSperre;
                    if (erlaubt == 0) continue;
                    long key = (long)ziel;
                    foreach (K m in mat.OrderBy(k => k)) key = key * 53 + (long)m;
                    key = key * 131 + maske % 7;
                    var mats = new int[bits];
                    int c = 0;
                    for (int i = 0; i < n; i++) if ((maske & (1 << i)) != 0) mats[c++] = i;
                    for (int zone = 0; zone < 7; zone++)
                    {
                        if ((erlaubt & (1 << zone)) == 0) continue;
                        // Rechte Extra-Monsterzone nur, wenn die linke gesperrt ist: gespiegelt gleichwertig
                        if (zone == 6 && (erlaubt & (1 << 5)) != 0) continue;
                        if (!gesehen.Add(key * 8 + zone + ((long)v.B[OM + mats[0]] << 50))) continue;
                        var s = new Schritt { Art = Art.Link, Karte = ziel, Ort = O.Extra, Zone = (sbyte)zone, Wahlen = matWahl.ToArray() };
                        if (!Erlaubt(s)) continue;
                        var z = v.Kopie(s);
                        for (int i = mats.Length - 1; i >= 0; i--) VomFeld(z, mats[i], false, true);
                        z.B[OD + (int)ziel]--;
                        if (ziel == K.Liberator) Nutze(z, E.LibSS);
                        if (ziel == K.Pisty) Nutze(z, E.PistyProc);
                        Rein(z, ziel, 0, zone);
                        NachLink(z, ziel);
                        Kind(aus, z);
                    }
                }
            }
        }

        private static void NachLink(Z z, K ziel)
        {
            if (ziel == K.Striker && Frei(z, E.StrikerS)) Neu(z, T.StrikerS);
            if (ziel == K.Breach && Frei(z, E.BreachS)) Neu(z, T.BreachS);
            if (ziel == K.Tris && Frei(z, E.TrisS)) Neu(z, T.TrisS);
            if (ziel == K.Twin && Frei(z, E.TwinR)) Neu(z, T.TwinR);
        }

        // --- Gegnerkarten ---------------------------------------------------------------------------

        private int GegnerZahl(Z z)
        {
            int n = 0;
            for (int i = 0; i < GegnerMonster.Count; i++) if ((z.B[CGegM] & (1 << i)) != 0) n++;
            return n;
        }
        private int BesterGegnerMonster(Z z, bool zerstoeren)
        {
            int best = -1, wert = -1;
            var gm = GegnerMonster;
            for (int i = 0; i < gm.Count; i++)
                if ((z.B[CGegM] & (1 << i)) != 0 && !gm[i].Immun && !(zerstoeren && gm[i].Unzerst) && gm[i].Wert > wert) { wert = gm[i].Wert; best = _gegner.IndexOf(gm[i]); }
            return best;
        }
        private int BesterGegnerNegierbar(Z z)
        {
            int best = -1, wert = -1;
            var gm = GegnerMonster;
            for (int i = 0; i < gm.Count; i++)
                if ((z.B[CGegM] & (1 << i)) != 0 && gm[i].Offen && !gm[i].Negiert && !gm[i].Immun && gm[i].Wert > wert) { wert = gm[i].Wert; best = _gegner.IndexOf(gm[i]); }
            return best;
        }
        private int BesteGegnerKarte(Z z)
        {
            int best = BesterGegnerMonster(z, true);
            int wert = best >= 0 ? _gegner[best].Wert : -1;
            var gz = GegnerZonen;
            for (int i = 0; i < gz.Count; i++)
                if ((z.B[CGegZ] & (1 << i)) != 0 && !gz[i].Unzerst && gz[i].Wert > wert) { wert = gz[i].Wert; best = _gegner.IndexOf(gz[i]); }
            return best;
        }
        private int BesteGegnerZone(Z z)
        {
            int best = -1, wert = -1;
            var gz = GegnerZonen;
            for (int i = 0; i < gz.Count; i++)
                if ((z.B[CGegZ] & (1 << i)) != 0 && gz[i].Wert > wert) { wert = gz[i].Wert; best = _gegner.IndexOf(gz[i]); }
            return best;
        }
        private Wahl GegnerWahl(int i) => new Wahl(K.Anderes, _gegner[i].Monster ? O.GegnerM : O.GegnerZ, i);
        private void EntferneGegner(Z z, int i)
        {
            var g = _gegner[i];
            int pos = g.Monster ? GegnerMonster.IndexOf(g) : GegnerZonen.IndexOf(g);
            if (g.Monster) z.B[CGegM] = (byte)(z.B[CGegM] & ~(1 << pos));
            else z.B[CGegZ] = (byte)(z.B[CGegZ] & ~(1 << pos));
        }

        // =====================================================================================
        // Auslöser auflösen
        // =====================================================================================

        private static void Weg(Z z, int index)
        {
            int last = --z.B[CP];
            for (int j = index; j < last; j++) z.B[OP + j] = z.B[OP + j + 1];
        }

        private void Ausloeser(Z v, List<Z> aus)
        {
            int gruppe = v.Gruppe;
            for (int i = 0; i < v.B[CP]; i++)
            {
                T t = (T)v.B[OP + i];
                Aufloesen(v, aus, i, t, gruppe);
                var skip = new Schritt { Art = Art.Auslassen, Karte = KarteVon(t), Ort = OrtVon(t), Gruppe = gruppe };
                var z = v.Kopie(skip);
                Weg(z, i);
                Kind(aus, z);
            }
        }

        private static K KarteVon(T t)
        {
            switch (t)
            {
                case T.LoaderS: return K.Loader;
                case T.StrikerS: return K.Striker;
                case T.BreachS: return K.Breach;
                case T.TrisS: return K.Tris;
                case T.FatalS: case T.FatalP: return K.Fatal;
                case T.AbsS: return K.Absorouter;
                case T.NoctoD: case T.NoctoH: return K.Nocto;
                case T.DillR: return K.Dilling;
                case T.RegenD: return K.Regen;
                case T.SynchR: return K.Synchron;
                default: return K.Twin;
            }
        }
        private static O OrtVon(T t)
        {
            switch (t)
            {
                case T.AbsS: case T.NoctoD: case T.DillR: case T.RegenD: case T.FatalP: return O.Grab;
                case T.NoctoH: return O.Hand;
                default: return O.Monster;
            }
        }

        private void Aufloesen(Z v, List<Z> aus, int i, T t, int gruppe)
        {
            K quelle = KarteVon(t);
            O ort = OrtVon(t);
            Schritt S(params Wahl[] w) => new Schritt { Art = Art.Ausloeser, Karte = quelle, Ort = ort, Gruppe = gruppe, Wahlen = w };
            void Suche(E e, IEnumerable<K> liste)
            {
                foreach (K k in liste)
                {
                    if (v.B[OD + (int)k] == 0) continue;
                    var z = v.Kopie(S(new Wahl(k, O.Deck))); Weg(z, i); Nutze(z, e);
                    z.B[OD + (int)k]--; z.B[OH + (int)k]++;
                    Kind(aus, z);
                }
            }
            switch (t)
            {
                case T.LoaderS:
                    {
                        // Die DUNKEL-Sperre kommt nur mit dem Effekt
                        foreach (K k in new[] { K.Detonator, K.Absorouter, K.Nocto })
                        {
                            if (v.B[OD + (int)k] == 0) continue;
                            var z = v.Kopie(S(new Wahl(k, O.Deck))); Weg(z, i); Nutze(z, E.LoaderS); Dunkel(z);
                            z.B[OD + (int)k]--; z.B[OH + (int)k]++;
                            Kind(aus, z);
                        }
                    }
                    return;
                case T.StrikerS: Suche(E.StrikerS, new[] { K.Boot }); return;
                case T.BreachS: Suche(E.BreachS, new[] { K.Regen, K.Reboot, K.Triple }); return;
                case T.TrisS: Suche(E.TrisS, new[] { K.Interlock, K.Rapid }); return;
                case T.FatalS: Suche(E.FatalS, new[] { K.Detonator, K.Barrage, K.Tracer, K.Caliber, K.Recharger, K.Loader, K.Silver, K.Magna }); return;
                case T.AbsS: Suche(E.AbsS, new[] { K.Barrage, K.Synchron, K.Tracer, K.Loader, K.Detonator, K.Caliber, K.Recharger, K.Silver, K.Magna, K.Shel, K.Auto }); return;
                case T.NoctoD:
                case T.RegenD:
                    {
                        var s = S(); s.Ziehen = true;
                        var z = v.Kopie(s); Weg(z, i); Nutze(z, t == T.NoctoD ? E.NoctoD : E.RegenD);
                        z.B[CGez]++;
                        Kind(aus, z);
                    }
                    return;
                case T.NoctoH:
                    if (v.B[OH + (int)K.Nocto] == 0 || !Platz(v)) return;
                    {
                        var z = v.Kopie(S()); Weg(z, i); Nutze(z, E.NoctoH);
                        z.B[OH + (int)K.Nocto]--;
                        Rein(z, K.Nocto);
                        Kind(aus, z);
                    }
                    return;
                case T.DillR:
                    if (v.B[OG + (int)K.Dilling] == 0 || !Platz(v)) return;
                    {
                        var z = v.Kopie(S()); Weg(z, i); Nutze(z, E.DillR);
                        GrabRaus(z, K.Dilling);
                        Rein(z, K.Dilling, FBann);
                        Kind(aus, z);
                    }
                    return;
                case T.FatalP:
                    {
                        int g = BesteGegnerKarte(v);
                        if (g >= 0)
                        {
                            var z = v.Kopie(S(GegnerWahl(g))); Weg(z, i); Nutze(z, E.FatalP);
                            EntferneGegner(z, g);
                            Kind(aus, z);
                        }
                        Ziele(v, _z);
                        foreach (var ziel in _z.ToList())
                        {
                            var z = v.Kopie(S(WahlFeld(v, ziel))); Weg(z, i); Nutze(z, E.FatalP);
                            Zerstoere(z, ziel);
                            Kind(aus, z);
                        }
                    }
                    return;
                case T.SynchR:
                    foreach (K k in new[] { K.Absorouter, K.Barrage, K.Detonator, K.Nocto, K.Magnamhut })
                    {
                        if (v.B[OG + (int)k] == 0 || !Platz(v)) continue;
                        var z = v.Kopie(S(new Wahl(k, O.Grab))); Weg(z, i); Nutze(z, E.SynchR); Dunkel(z);
                        GrabRaus(z, k);
                        Rein(z, k, (byte)(FNegiert | FEnde));
                        Kind(aus, z);
                    }
                    return;
                case T.TwinR:
                    foreach (K k in new[] { K.Absorouter, K.Barrage, K.Detonator, K.Nocto, K.Magnamhut, K.Fatal, K.Furious })
                    {
                        if (v.B[OG + (int)k] == 0) continue;
                        int pos = -1;
                        for (int m = 0; m < v.B[CM]; m++) if ((K)v.B[OM + m] == K.Twin) pos = v.B[OMP + m];
                        if (pos < 0) continue;
                        int zone = ZoneMain(FreiMain(v) & Zeigt(K.Twin, pos), 0);
                        if (zone < 0) continue;
                        var z = v.Kopie(S(new Wahl(k, O.Grab))); Weg(z, i); Nutze(z, E.TwinR);
                        GrabRaus(z, k);
                        Rein(z, k, (byte)(FNegiert | FKeinAngriff), zone);
                        Kind(aus, z);
                    }
                    return;
            }
        }

        // =====================================================================================
        // Bewertung
        // =====================================================================================

        private static int WertFeld(K k)
        {
            switch (k)
            {
                case K.End: return 80;
                case K.Shroud: return 80;
                case K.Liberator: return 34;
                case K.Sword: return 30;
                case K.Furious: return 38;
                case K.Fatal: return 22;
                case K.Tris: return 22;
                case K.Code: return 18;
                case K.Breach: return 18;
                case K.Dilling: return 14;
                case K.Twin: return 8;
                case K.Pisty: return 6;
                case K.Striker: return 5;
                case K.Magna: return 15;
                case K.Barrage: return 12;
                case K.Silver: return 11;
                case K.Shel: return 11;
                case K.Tracer: return 12;
                case K.Recharger: return 12;
                case K.Auto: return 9;
                case K.Magnamhut: return 12;
                case K.Detonator: return 10;
                case K.Absorouter: return 9;
                case K.Loader: return 7;
                case K.Caliber: return 7;
                case K.Nocto: return 7;
                default: return 3;
            }
        }
        private static int WertHand(K k)
        {
            switch (k)
            {
                case K.Droplet: return 18;
                case K.Imperm: return 17;
                case K.Reboot: return 16;
                case K.Rapid: return 16;
                case K.Triple: return 12;
                case K.Launch: return 11;
                case K.Recharger: return 10;
                case K.Caliber: return 7;
                case K.Detonator: return 6;
                case K.Nocto: return 6;
                case K.Barrage: return 5;
                case K.Absorouter: return 5;
                case K.Boot: return 5;
                case K.Regen: return 4;
                case K.Interlock: return 3;
                default: return 4;
            }
        }

        /// <summary>Wert des Zustands, wenn der Zug jetzt enden würde.</summary>
        internal int Bewerte(Z z)
        {
            int w = 0;
            bool end = false, shroud = false;
            int atk = 0;
            var angreifer = new List<int>();
            for (int i = 0; i < z.B[CM]; i++)
            {
                K k = (K)z.B[OM + i];
                byte f = z.B[OMF + i];
                if ((f & FEnde) != 0) { if (IstSchwimmer(k)) w += 10; continue; }     // End Phase: weg, Schwimmer holt Ersatz
                w += WertFeld(k);
                if ((f & FNegiert) != 0) w -= 4;
                if (k == K.End) end = true;
                if (k == K.Shroud) shroud = true;
                if ((f & FKeinAngriff) == 0) { angreifer.Add(Atk(k)); atk += Atk(k); }
            }
            if (end && shroud) w += 120;
            else if (end || shroud) w += 25;
            // Borrelend braucht einen Rokket im Friedhof, Borrelshroud einen Rokket auf dem Feld
            if (end && Rokkets.Any(k => z.B[OG + (int)k] > 0)) w += 12;
            if (shroud)
                for (int i = 0; i < z.B[CM]; i++) if (IstRokket((K)z.B[OM + i]) && (z.B[OMF + i] & FEnde) == 0) { w += 10; break; }
            for (int i = 0; i < z.B[CS]; i++) w += (K)z.B[OS + i] == K.Regen ? 2 : 3;
            if (z.B[CFeld] != 0) w += 3;
            for (int k = 0; k < NK; k++) if (z.B[OH + k] > 0) w += z.B[OH + k] * WertHand((K)k);
            w += z.B[CGez] * 10;
            // Friedhof: Liberator kommt im nächsten Zug zurück, Dillingerous bei jedem Rokket, Furious belebt einen Link
            if (z.B[OG + (int)K.Liberator] > 0 && Frei(z, E.LibSS)) w += 14;
            if (z.B[OG + (int)K.Dilling] > 0) w += 8;
            if (z.B[OG + (int)K.Furious] > 0) w += 6;
            if (z.B[OG + (int)K.Nocto] > 0) w += 3;
            // End Phase: zerstörte Schwimmer holen je 1 Rokket aus dem Deck
            int frei = Math.Max(0, 5 - System.Linq.Enumerable.Range(0, 5).Count(p => (Belegt(z) & (1 << p)) != 0));
            int schwimmer = 0;
            foreach (K k in new[] { K.Silver, K.Magna, K.Shel, K.Auto })
                if (z.B[OZ + (int)k] > 0 && z.B[OG + (int)k] > 0) schwimmer++;
            w += Math.Min(schwimmer, frei) * 11;
            // Gegner
            var gm = GegnerMonster; var gz = GegnerZonen;
            for (int i = 0; i < gm.Count; i++) if ((z.B[CGegM] & (1 << i)) == 0) w += gm[i].Wert;
            for (int i = 0; i < gz.Count; i++) if ((z.B[CGegZ] & (1 << i)) == 0) w += gz[i].Wert;
            // Schaden: im zweiten Zug zählt, was durchkommt
            if (!_ersterZug && !_hauptphase2)
            {
                int blocker = 0;
                for (int i = 0; i < gm.Count; i++) if ((z.B[CGegM] & (1 << i)) != 0) blocker++;
                // Borrelshroud holt zu Beginn der Battle Phase Liberator, der stiehlt das beste Gegnermonster
                if (shroud && z.B[OD + (int)K.Liberator] > 0 && Frei(z, E.LibSS)) { angreifer.Add(3000); if (blocker > 0) blocker--; }
                if (end && blocker > 0) blocker = Math.Max(0, blocker - 2);   // Borrelend greift alle Monster an
                int dmg = angreifer.OrderByDescending(a => a).Skip(blocker).Sum();
                w += Math.Min(dmg, _gegnerLP) / 100;
                if (dmg >= _gegnerLP) w += 300;
            }
            return w;
        }

        private static int Atk(K k)
        {
            switch (k)
            {
                case K.End: case K.Shroud: return 3500;
                case K.Liberator: case K.Sword: case K.Zero: case K.Furious: return 3000;
                case K.Fatal: case K.Code: case K.Magnamhut: return 2500;
                case K.Tris: return 2300;
                case K.Detonator: return 1800;
                case K.Magna: return 1800;
                case K.Silver: return 1900;
                case K.Caliber: return 1700;
                case K.Dilling: case K.Tracer: case K.Auto: return 1600;
                case K.Loader: case K.Barrage: return 1500;
                case K.Breach: case K.Twin: case K.Absorouter: return 1200;
                case K.Shel: return 1100;
                case K.Striker: case K.Pisty: return 1000;
                default: return 0;
            }
        }

        /// <summary>Bewertung für die Strahlsuche: Endwert plus Guthaben für Material und freie Effekte, sonst fliegen Zwischenzustände zu früh raus.</summary>
        private int Rang(Z z)
        {
            int w = Bewerte(z);
            foreach (E e in FreieMotor) if (Frei(z, e)) w += 1;
            for (int i = 0; i < z.B[CP]; i++) w += 10;
            for (int i = 0; i < z.B[CQ]; i++) w += 10;
            // Link-Wert auf dem Feld und Körper zählen als Fortschritt zum zweiten Link-5
            int links = 0, koerper = 0;
            for (int i = 0; i < z.B[CM]; i++)
            {
                K k = (K)z.B[OM + i];
                if (IstLink(k)) links += LinkWert(k);
                else koerper++;
                if ((z.B[OMF + i] & FNegiert) == 0)
                {
                    if (k == K.Tracer && Frei(z, E.TracerEff)) w += 6;
                    if (k == K.Breach && Frei(z, E.BreachEff)) w += 6;
                    if (k == K.Fatal && Frei(z, E.FatalEquip)) w += 6;
                    if (k == K.Detonator && Frei(z, E.DetEff)) w += 4;
                }
            }
            w += links * 3 + koerper * 5;
            // Handkarten, die noch Körper bringen
            foreach (K k in new[] { K.Launch, K.Triple, K.Boot, K.Regen, K.Interlock, K.Barrage, K.Absorouter, K.Detonator, K.Caliber })
                if (z.B[OH + (int)k] > 0) w += 4;
            return w;
        }
        private static readonly E[] FreieMotor = { E.BreachEff, E.BootEff, E.Interlock, E.TrisEff, E.EndEff, E.SilverZ, E.BarrageZ, E.TripleA, E.RegenAkt, E.DetEff, E.FatalEquip };

        // =====================================================================================
        // Suche
        // =====================================================================================

        /// <summary>Strahlsuche über Zugfolgen, von schmal nach breit: Eine schmale Suche kommt schnell bis zum Zugende, die breiten holen das Beste heraus,
        /// solange Zeit ist. Liefert den besten Plan (Schritte in Reihenfolge) und seinen Wert.</summary>
        internal List<Z> Suche(Z start, int breite, int millis, out int wert)
        {
            var uhr = Stopwatch.StartNew();
            Z best = start;
            int bestWert = start.B[CP] > 0 ? int.MinValue : Bewerte(start);
            var kinder = new List<Z>();
            for (int b = Math.Max(100, breite / 27); b <= breite; b *= 3)
            {
                var gesehen = new HashSet<ulong>();
                var schicht = new List<Z> { start };
                for (int tiefe = 0; tiefe < 90 && schicht.Count > 0; tiefe++)
                {
                    kinder.Clear();
                    foreach (Z v in schicht)
                    {
                        int vor = kinder.Count;
                        Erweitere(v, kinder);
                        _knoten += kinder.Count - vor;
                        if (uhr.ElapsedMilliseconds > millis) break;
                    }
                    var neu = new List<(int rang, Z z)>(kinder.Count);
                    foreach (Z k in kinder)
                    {
                        if (!gesehen.Add(k.Hash())) continue;
                        if (k.B[CP] == 0)
                        {
                            int w = Bewerte(k);
                            if (w > bestWert) { bestWert = w; best = k; }
                        }
                        neu.Add((Rang(k), k));
                    }
                    if (uhr.ElapsedMilliseconds > millis) break;
                    neu.Sort((x, y) => y.rang.CompareTo(x.rang));
                    // Plätze für Zustände ohne Link-5 freihalten: Sonst verdrängt ein frühes Borrelend die längeren Wege zu beiden (Tracer allein, Video 03.11.)
                    schicht = neu.Take(b / 2).Select(x => x.z).Concat(neu.Skip(b / 2).Where(x => !HatLink5(x.z)).Take(b - b / 2).Select(x => x.z)).ToList();
                }
                if (uhr.ElapsedMilliseconds > millis) break;
            }
            wert = bestWert;
            var pfad = new List<Z>();
            for (Z z = best; z != null && z.Schritt != null; z = z.Vater) pfad.Add(z);
            pfad.Reverse();
            return pfad;
        }
    }
}
