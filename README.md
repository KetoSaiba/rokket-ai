# 🐉 ROKKET AI für EDOPro

Eine Computer-KI für **EDOPro (Project Ignis)**, die das Deck **„Rokket“** spielt (Borrelend Dragon + Borrelshroud Dragon) – gratis, mit offenem Quellcode.
Du kannst gegen sie spielen oder sie gegen andere KIs antreten lassen.

📺 Die KI in Aktion: YouTube **[Keto Saiba](https://www.youtube.com/@KetoSaibaYGO)** · Instagram **[@ketosaiba.ygo](https://www.instagram.com/ketosaiba.ygo)** · Patreon **[Keto Saiba](https://www.patreon.com/KetoSaibaYgo)**

> *English: A free, open-source WindBot AI for EDOPro that plays the "Rokket" deck (Borrelend + Borrelshroud). Download the ZIP under **Releases** and follow `ANLEITUNG.txt` in the ZIP (German).*

## ⬇️ Download
Rechts unter **Releases** die neueste `Rokket-KI-fuer-EDOPro-…….zip` herunterladen. Die Anleitung (`ANLEITUNG.txt`) liegt im ZIP – kurz:

1. EDOPro einmal starten (Updates laden lassen), dann schließen.
2. ZIP: Rechtsklick → Eigenschaften → „Zulassen“ anhaken (falls vorhanden) → entpacken.
3. Den Inhalt von `In-ProjectIgnis-kopieren` in deinen `ProjectIgnis`-Ordner ziehen.
4. Den Text aus `bots-json-Eintrag.txt` in `ProjectIgnis\WindBot\bots.json` direkt unter die erste Zeile `[` einfügen.
5. In EDOPro einen Raum erstellen → „KI hinzufügen“ → **ROKKET AI**. Das Deck nutzt auch OCG-Karten, also Prerelease/alle Karten erlauben oder „Deck nicht überprüfen“ anhaken.

## 🧠 Was die KI kann
- **Eigener Zugplaner mit Zonen und Link-Pfeilen:** Sie probiert von der echten Hand aus Zugfolgen durch und sucht den Weg zu **beiden Link-5 auf einem Feld** (Borrelend Dragon + Borrelshroud Dragon)
- Als Erster schafft sie beide Link-5 in rund 3 von 4 Duellen, oft noch mit gesetzten Borrel-Fallen dahinter
- Merkt sich verdeckte Karten, die vorher offen waren, und legt keine Zerstörungseffekte auf Karten, die nicht zerstört werden können
- 55,6 % Siege in 880 Turnier-Duellen gegen 11 andere KI-Decks (als Erster 67,7 %, als Zweiter 43,4 %). Das sagt nichts über die Stärke gegen Menschen aus

## 🛠️ Selbst bauen (für Programmierer)
- Quellcode: `src/` (C#, .NET Framework 4.8)
- `ExecutorBase.dll` aus deinem `ProjectIgnis\WindBot`-Ordner nach `src/lib/` kopieren
- `dotnet build -c Release` in `src/` → `RokketAI.dll` nach `ProjectIgnis\WindBot\Executors`
- Decks: `deck/AI_RokketAI.ydk` (Deck der KI), `deck/Rokkets.ydk` (zum Selberspielen)

Version: 2026.11.21

## ❤️ Unterstützen
Die KI ist und bleibt **kostenlos**. Wenn du die Arbeit trotzdem unterstützen möchtest: auf **[Patreon](https://www.patreon.com/KetoSaibaYgo)** – ganz freiwillig, du bekommst dafür keine zusätzlichen Inhalte an der KI.

## ⚖️ Lizenz & Hinweise
- Lizenz: **GNU AGPL-3.0** (siehe `LICENSE`). Die KI baut auf [WindBot Ignite](https://github.com/ProjectIgnis/windbot) von Project Ignis auf, das ebenfalls unter der AGPL-3.0 steht. Du darfst den Code nutzen, ändern und weitergeben, solange du deine Änderungen ebenfalls unter der AGPL-3.0 mit Quellcode veröffentlichst.
- **Inoffizielles Fan-Projekt.** Keine Verbindung zu Konami, Shueisha, Studio Dice oder Project Ignis. „Yu-Gi-Oh!“ sowie alle Karten- und Monsternamen sind Marken bzw. Eigentum ihrer jeweiligen Rechteinhaber. Dieses Projekt enthält keine Kartenbilder, Kartentexte oder Spieldateien – nur die KI-Logik und Decklisten.
- Ohne Gewähr: Nutzung auf eigene Verantwortung.
