// Erzeugt von immun_tabelle.py aus den Kartentexten, nicht von Hand ändern.
using System.Collections.Generic;

namespace WindBot.Game.AI.Decks
{
    internal static partial class ZerstoerSchutz
    {
        /// <summary>Unberührt von Effekten („unaffected by“). Bits der Quelle: 1 Monster, 2 Zauber, 4 Fallen. Art siehe immun_tabelle.py.</summary>
        private static readonly Dictionary<int, Immunitaet> ImmunDaten = new Dictionary<int, Immunitaet>
        {
            { 952523, new Immunitaet(4, 0, 0) },  // Traptrix Cularia
            { 1340142, new Immunitaet(7, 1, 0) },  // Varar, Vaalmonican Concord
            { 1516510, new Immunitaet(7, 1, 0) },  // Rune-Eyes Pendulum Dragon
            { 1566817, new Immunitaet(1, 0, 0) },  // Cupid Serve
            { 1688285, new Immunitaet(5, 1, 0) },  // Traptrix Pinguicula
            { 2095764, new Immunitaet(4, 0, 0) },  // Bixi, Water of the Yang Zing
            { 2204038, new Immunitaet(2, 0, 0) },  // Valkyrie Brunhilde
            { 3405259, new Immunitaet(5, 1, 0) },  // C-Crush Wyvern
            { 3643300, new Immunitaet(2, 1, 0) },  // The Legendary Fisherman
            { 4063756, new Immunitaet(7, 1, 0) },  // Medicurius the Power Patron of Illusions
            { 6075801, new Immunitaet(1, 1, 0) },  // Paladin of Felgrand
            { 6195332, new Immunitaet(7, 1, 0) },  // Exstellarknight Constellar Ptolemy Ω7
            { 6511113, new Immunitaet(4, 1, 0) },  // Traptrix Rafflesia
            { 9633505, new Immunitaet(2, 0, 0) },  // Guardian Kay'est
            { 10000090, new Immunitaet(7, 0, 0) },  // The Winged Dragon of Ra - Immortal Phoenix
            { 10669138, new Immunitaet(7, 0, 0) },  // Five-Headed Link Dragon
            { 10817524, new Immunitaet(1, 0, 0) },  // First of the Dragons
            { 11224103, new Immunitaet(2, 0, 0) },  // Horus the Black Flame Dragon LV6
            { 11443677, new Immunitaet(4, 0, 0) },  // Blue-Eyes Tyrant Dragon
            { 11464648, new Immunitaet(7, 1, 0) },  // Fiendsmith's Rextremende
            { 11516241, new Immunitaet(7, 0, 0) },  // Gouki The Powerload Ogre
            { 11738489, new Immunitaet(7, 0, 0) },  // The Arrival Cyberse @Ignister
            { 13023431, new Immunitaet(7, 1, 0) },  // Chevreuil, Hunting Scout of the Deep Forest
            { 13073850, new Immunitaet(1, 1, 0) },  // Qliphort Stealth
            { 13203964, new Immunitaet(1, 0, 0) },  // Perfectron Hydradrive Dragon
            { 13836592, new Immunitaet(7, 1, 0) },  // Behemoth the King of a Hundred Battles
            { 15721123, new Immunitaet(1, 1, 0) },  // Confronting the "C"
            { 17242022, new Immunitaet(3, 0, 0) },  // Red-Eyes Black Dragon Exceed
            { 17242023, new Immunitaet(3, 0, 0) },  // Red-Eyes Black Dragon Exceed
            { 18743376, new Immunitaet(5, 1, 0) },  // Metaphys Tyrant Dragon
            { 18988396, new Immunitaet(7, 0, 0) },  // Miracle Raven
            { 19700943, new Immunitaet(7, 1, 0) },  // Dododo Bot
            { 19801646, new Immunitaet(1, 1, 0) },  // The Legendary Fisherman II
            { 22423493, new Immunitaet(7, 0, 0) },  // Qliphort Genius
            { 23219323, new Immunitaet(1, 0, 0) },  // Junk Warrior/Assault Mode
            { 23220863, new Immunitaet(7, 0, 0) },  // Magical Cavalry of Cxulub
            { 23971061, new Immunitaet(1, 0, 0) },  // Doublebyte Dragon
            { 24128274, new Immunitaet(2, 1, 0) },  // Deepsea Warrior
            { 26746975, new Immunitaet(3, 1, 0) },  // Dark Guardian
            { 27240101, new Immunitaet(7, 0, 0) },  // Kikinagashi Fucho
            { 27279764, new Immunitaet(7, 1, 0) },  // Apoqliphort Towers
            { 27632520, new Immunitaet(7, 0, 0) },  // Fairy Tail - Wiccat
            { 30012506, new Immunitaet(1, 1, 0) },  // A-Assault Core
            { 30270176, new Immunitaet(7, 4, 3000) },  // Crimson Nova the Dark Cubic Lord
            { 30492798, new Immunitaet(4, 0, 0) },  // Djinn Disserere of Rituals
            { 32467459, new Immunitaet(7, 0, 0) },  // El Shaddoll Meshachrer
            { 35089369, new Immunitaet(2, 0, 0) },  // Pulao, Wind of the Yang Zing
            { 36346532, new Immunitaet(1, 0, 0) },  // Paleozoic Cambroraster
            { 36898537, new Immunitaet(7, 1, 0) },  // Metaphys Horus
            { 37542782, new Immunitaet(7, 0, 0) },  // Cyberdark End Dragon
            { 37649320, new Immunitaet(1, 0, 0) },  // Paleozoic Opabinia
            { 37991342, new Immunitaet(1, 1, 0) },  // Qliphort Helix
            { 39475024, new Immunitaet(7, 1, 0) },  // Gashadokuro, the Skeletal Mayakashi
            { 40061558, new Immunitaet(7, 1, 0) },  // Apoqliphort Skybase
            { 41147577, new Immunitaet(1, 0, 0) },  // CXyz Simon the Great Moral Leader
            { 41302052, new Immunitaet(7, 1, 0) },  // Trickstar Bella Madonna
            { 41373230, new Immunitaet(7, 1, 0) },  // Titaniklad the Ash Dragon
            { 41517789, new Immunitaet(7, 1, 0) },  // Star Eater
            { 41589166, new Immunitaet(4, 0, 0) },  // Tenkabito Shien
            { 42592719, new Immunitaet(2, 1, 0) },  // Gladiator Beast Alexander
            { 42632209, new Immunitaet(7, 1, 0) },  // Geomathmech Final Sigma
            { 43047672, new Immunitaet(7, 1, 0) },  // Raidraptor - Final Fortress Falcon
            { 44001993, new Immunitaet(7, 1, 0) },  // Magician of Dark Chaos - Black Chaos
            { 44001994, new Immunitaet(7, 1, 0) },  // Magician of Dark Chaos - Black Chaos
            { 44968687, new Immunitaet(6, 0, 0) },  // The Legendary Fisherman III
            { 46955770, new Immunitaet(7, 1, 0) },  // Gravekeeper's Heretic
            { 47195442, new Immunitaet(7, 0, 0) },  // Wonky Quartet
            { 47946130, new Immunitaet(7, 0, 0) },  // Gouki The Giant Ogre
            { 48183890, new Immunitaet(4, 0, 0) },  // Traptrix Atypus
            { 48348921, new Immunitaet(1, 1, 0) },  // Number C62: Neo Galaxy-Eyes Prime Photon Dragon
            { 48469380, new Immunitaet(1, 1, 0) },  // Archfiend Emperor
            { 49441499, new Immunitaet(2, 1, 0) },  // Ultimate Insect LV1
            { 50855622, new Immunitaet(7, 1, 0) },  // Ultra Beetrooper Absolute Hercules
            { 51788412, new Immunitaet(6, 0, 0) },  // Chaos Ancient Gear Giant
            { 52085072, new Immunitaet(7, 1, 0) },  // Dystopia the Despondent
            { 53315891, new Immunitaet(7, 0, 0) },  // Timaeus the Knight of Destiny
            { 53466722, new Immunitaet(1, 0, 0) },  // First of the Dragonlords
            { 54082269, new Immunitaet(7, 0, 0) },  // Blackwing Full Armor Master
            { 54701958, new Immunitaet(7, 0, 0) },  // Lunalight Liger Dancer
            { 55787576, new Immunitaet(1, 0, 0) },  // World Legacy - "World Shield"
            { 55863245, new Immunitaet(1, 0, 0) },  // Tatsunoko
            { 57043117, new Immunitaet(1, 0, 0) },  // Giant Soldier of Steel
            { 57282724, new Immunitaet(1, 0, 0) },  // World Gears of Theurlogical Demiurgy
            { 58604027, new Immunitaet(7, 0, 0) },  // The Legendary Exodia Incarnate
            { 59071624, new Immunitaet(4, 1, 0) },  // Traptrix Allomerus
            { 59120809, new Immunitaet(7, 0, 0) },  // Scareclaw Tri-Heart
            { 59765225, new Immunitaet(7, 1, 0) },  // Crystal Clear Wing Synchro Dragon
            { 60465049, new Immunitaet(7, 1, 0) },  // Psychic End Punisher
            { 61307542, new Immunitaet(1, 0, 0) },  // Paleozoic Anomalocaris
            { 61307543, new Immunitaet(1, 0, 0) },  // Paleozoic Anomalocaris
            { 63504681, new Immunitaet(7, 1, 0) },  // Number 86: Heroic Champion - Rhongomyniad
            { 64496451, new Immunitaet(1, 1, 0) },  // Qliphort Disk
            { 65029288, new Immunitaet(7, 1, 0) },  // Performapal Odd-Eyes Metal Claw
            { 65884091, new Immunitaet(1, 1, 0) },  // Evilswarm Thanatos
            { 67359907, new Immunitaet(7, 1, 0) },  // Disaster, Dragon Ruler of All Apocalypses
            { 67547370, new Immunitaet(7, 0, 0) },  // Zushin the Sleeping Giant
            { 67712104, new Immunitaet(7, 1, 0) },  // Marincess Crystal Heart
            { 69073023, new Immunitaet(1, 0, 0) },  // Infinitrack Fortress Megaclops
            { 70405001, new Immunitaet(7, 1, 0) },  // Black Luster Soldier - Soldier of Light and Darkness
            { 70405002, new Immunitaet(7, 1, 0) },  // Black Luster Soldier - Soldier of Light and Darkness
            { 70488851, new Immunitaet(1, 0, 0) },  // Vidolium the Unstable Power Patron of Unity
            { 71222868, new Immunitaet(7, 0, 0) },  // Raidraptor - Rising Rebellion Falcon
            { 71555408, new Immunitaet(7, 1, 0) },  // Gouki Iron Claw
            { 72144675, new Immunitaet(4, 0, 0) },  // Troposphere
            { 72443568, new Immunitaet(2, 0, 0) },  // Silent Magician LV8
            { 72443569, new Immunitaet(2, 0, 0) },  // Silent Magician LV8
            { 73639099, new Immunitaet(4, 0, 0) },  // Traptrix Sera
            { 74388798, new Immunitaet(2, 0, 0) },  // Silent Swordsman LV5
            { 76666602, new Immunitaet(7, 1, 0) },  // The Dragon that Devours the Dogma
            { 76815942, new Immunitaet(7, 0, 0) },  // Lyrilusc - Independent Nightingale
            { 77411244, new Immunitaet(3, 1, 0) },  // B-Buster Drake
            { 78744660, new Immunitaet(1, 1, 0) },  // Archfiend Matriarch
            { 79194594, new Immunitaet(7, 1, 0) },  // Myutant Synthesis
            { 82697249, new Immunitaet(7, 1, 0) },  // Number 59: Crooked Cook
            { 83827392, new Immunitaet(7, 1, 0) },  // Expurrely Noir
            { 85899505, new Immunitaet(7, 1, 0) },  // Timaeus the United Magical Dragon
            { 86038337, new Immunitaet(7, 1, 0) },  // Faisan, Hunting Scout of the Deep Forest
            { 86188410, new Immunitaet(4, 0, 0) },  // Elemental HERO Wildheart
            { 86221741, new Immunitaet(7, 0, 0) },  // Raidraptor - Ultimate Falcon
            { 86274272, new Immunitaet(7, 1, 0) },  // Ritual Beast Ulti-Apelio
            { 86605184, new Immunitaet(7, 1, 0) },  // Firewall Guardian
            { 87182127, new Immunitaet(7, 0, 0) },  // Ancient Gear Howitzer
            { 87588741, new Immunitaet(1, 1, 0) },  // Qliphort Cephalopod
            { 89474727, new Immunitaet(7, 1, 0) },  // Stardust Chronicle Spark Dragon
            { 89571015, new Immunitaet(1, 0, 0) },  // Storm Cipher
            { 90337190, new Immunitaet(2, 1, 0) },  // Torpedo Fish
            { 90885155, new Immunitaet(1, 1, 0) },  // Qliphort Shell
            { 91073013, new Immunitaet(7, 1, 0) },  // Vanquish Soul Caesar Valius
            { 91073014, new Immunitaet(7, 1, 0) },  // Vanquish Soul Caesar Valius
            { 91123920, new Immunitaet(2, 0, 0) },  // The Agent of Force - Mars
            { 91907707, new Immunitaet(1, 1, 0) },  // Qliphort Carrier
            { 93449450, new Immunitaet(7, 5, 0) },  // F.A. Hang On Mach
            { 95243515, new Immunitaet(7, 1, 0) },  // Virtual World Tiger - Huhu
            { 95614612, new Immunitaet(2, 1, 0) },  // Cannonball Spear Shellfish
            { 98127546, new Immunitaet(7, 1, 0) },  // Saros Eresh Kurnugias
            { 98127547, new Immunitaet(7, 1, 0) },  // Underworld Goddess of the Closed World
            { 98684220, new Immunitaet(7, 1, 0) },  // Black Chaos
            { 100000110, new Immunitaet(7, 0, 0) },  // Zushin the Sleeping Giant (VG)
            { 100000538, new Immunitaet(2, 0, 0) },  // Valkyrie Brunhilde (Anime)
            { 100001010, new Immunitaet(4, 0, 0) },  // Litmus Doom Swordsman (VG)
            { 100430021, new Immunitaet(7, 1, 0) },  // Vanquish Soul - Kaiser Varius
            { 100458003, new Immunitaet(7, 1, 0) },  // Zenetnakht Sphinx
            { 100458022, new Immunitaet(7, 1, 0) },  // Cielo, the Night Sky of Raise Moon - No More Bets!
            { 100459004, new Immunitaet(7, 2, 0) },  // Blue-Eyes Ultimate Dragon, the God-Surpassing Servant
            { 100460003, new Immunitaet(1, 3, 0) },  // Abyss Vagrada the Abyssal Cubic Lord
            { 100460004, new Immunitaet(1, 3, 0) },  // Divine Anima Infinity the Creation Cubic Overlord
            { 101104046, new Immunitaet(4, 0, 0) },  // Traptrix Utricularia
            { 101111043, new Immunitaet(1, 1, 0) },  // Number C62: Neo Galaxy-Eyes Prime Photon Dragon
            { 101403030, new Immunitaet(7, 0, 0) },  // The Sun God of Darkness - The Winged Dragon of Ra
            { 101403035, new Immunitaet(7, 0, 0) },  // Omega Vidolium the Power Patron of Heaven's End
            { 101403130, new Immunitaet(7, 0, 0) },  // The Sun God of Darkness - The Winged Dragon of Ra
            { 140000083, new Immunitaet(6, 0, 0) },  // Air Fortress Ziggurat
            { 153000003, new Immunitaet(2, 1, 0) },  // Deepsea Warrior (Deck Master)
            { 504700015, new Immunitaet(2, 1, 0) },  // Horus the Black Flame Dragon LV6 (GOAT)
            { 504700081, new Immunitaet(2, 1, 0) },  // Ultimate Insect LV1 (GOAT)
            { 504700126, new Immunitaet(2, 0, 0) },  // Silent Swordsman LV5 (GOAT)
            { 511000175, new Immunitaet(2, 0, 0) },  // Necro Jar
            { 511000234, new Immunitaet(6, 1, 0) },  // Uria, Lord of Searing Flames (Anime)
            { 511000239, new Immunitaet(6, 1, 0) },  // Earthbound Immortal Aslla piscu (Anime)
            { 511000242, new Immunitaet(6, 1, 0) },  // Earthbound Immortal Uru (Anime)
            { 511000243, new Immunitaet(7, 0, 0) },  // Exodius the Ultimate Forbidden Lord (Anime)
            { 511000246, new Immunitaet(6, 1, 0) },  // Hamon, Lord of Striking Thunder (Anime)
            { 511000250, new Immunitaet(6, 1, 0) },  // Earthbound Immortal Cusillu (Anime)
            { 511000252, new Immunitaet(6, 1, 0) },  // Earthbound Immortal Wiraqocha Rasca (Anime)
            { 511000255, new Immunitaet(6, 1, 0) },  // Earthbound Immortal Ccapac Apu (Anime)
            { 511000261, new Immunitaet(6, 0, 0) },  // Raviel, Lord of Phantasms (Anime)
            { 511000262, new Immunitaet(6, 1, 0) },  // Earthbound Immortal Chacu Challhua (Anime)
            { 511000263, new Immunitaet(6, 1, 0) },  // Earthbound Immortal Ccarayhua (Anime)
            { 511000389, new Immunitaet(7, 1, 0) },  // Forbidden Beast Bronn
            { 511001372, new Immunitaet(1, 0, 0) },  // Number C88: Gimmick Puppet Disaster Leo (Anime)
            { 511001608, new Immunitaet(7, 1, 0) },  // Number 93: Utopia Kaiser (Manga)
            { 511002079, new Immunitaet(7, 0, 0) },  // Raidraptor - Ultimate Falcon (Anime)
            { 511002110, new Immunitaet(7, 0, 0) },  // Vennominaga the Deity of Poisonous Snakes (Anime)
            { 511008508, new Immunitaet(7, 0, 0) },  // Castle Gate (Manga)
            { 511009307, new Immunitaet(7, 1, 0) },  // Ancient Gear Howitzer (Anime)
            { 511009441, new Immunitaet(7, 1, 0) },  // Supreme King Z-ARC (Anime)
            { 511009645, new Immunitaet(2, 1, 0) },  // Mirror Imagine Prism Coat 8
            { 511009651, new Immunitaet(7, 1, 0) },  // Trickstar Bella Madonna (Anime)
            { 511010053, new Immunitaet(7, 0, 0) },  // Number 53: Heart-eartH (Anime)
            { 511016002, new Immunitaet(7, 1, 0) },  // Arcana Force 0 - The Fool (Anime)
            { 511016006, new Immunitaet(7, 1, 0) },  // Neo Blue-Eyes Ultimate Dragon (Anime)
            { 511027010, new Immunitaet(7, 1, 0) },  // Paradox Hydradrive Atlas
            { 511030011, new Immunitaet(7, 1, 0) },  // Armatos Legio Legatus Legionis
            { 511310001, new Immunitaet(7, 1, 0) },  // Dododo Warrior (Anime)
            { 511600030, new Immunitaet(1, 0, 0) },  // Obelisk The Progenitor
            { 511600068, new Immunitaet(7, 1, 0) },  // Viser Des (Manga)
            { 511600215, new Immunitaet(7, 0, 0) },  // Cyberse Wicckid (Anime)
            { 511600260, new Immunitaet(7, 1, 0) },  // Marincess Crystal Heart (Anime)
            { 511600267, new Immunitaet(7, 1, 0) },  // Marincess Wonder Heart (Anime)
            { 511600300, new Immunitaet(7, 1, 0) },  // The Arrival Cyberse @Ignister (Anime)
            { 511600352, new Immunitaet(7, 1, 0) },  // Gatchiri @Ignister (Anime)
            { 511600398, new Immunitaet(7, 1, 0) },  // Obelisk the Tormentor (Anime)
            { 511600399, new Immunitaet(7, 1, 0) },  // Slifer the Sky Dragon (Anime)
            { 511600400, new Immunitaet(7, 0, 0) },  // The Winged Dragon of Ra (Anime)
            { 513000031, new Immunitaet(1, 0, 0) },  // Shooting Quasar Dragon (Anime)
            { 513000134, new Immunitaet(7, 0, 0) },  // The Winged Dragon of Ra (Manga)
            { 513000135, new Immunitaet(1, 0, 0) },  // Obelisk the Tormentor (Manga)
            { 513000136, new Immunitaet(1, 0, 0) },  // Slifer the Sky Dragon (Manga)
            { 513000137, new Immunitaet(1, 0, 0) },  // The Wicked Dreadroot (Manga)
            { 513000138, new Immunitaet(1, 0, 0) },  // The Wicked Avatar (Manga)
            { 513000139, new Immunitaet(1, 0, 0) },  // The Wicked Eraser (Manga)
        };
        /// <summary>Kann ohne Bedingung nicht als Ziel dieser Quellen gewählt werden.</summary>
        private static readonly Dictionary<int, byte> KeinZielBits = new Dictionary<int, byte>
        {
            { 1409474, 7 },  // Hazy Flame Sphynx
            { 6165656, 7 },  // Number C88: Gimmick Puppet Disaster Leo
            { 7574904, 4 },  // Myutant Arsenal
            { 8561192, 7 },  // Leo, the Keeper of the Sacred Tree
            { 8696773, 7 },  // Hazy Flame Hydra
            { 10000080, 7 },  // The Winged Dragon of Ra - Sphere Mode
            { 10136446, 7 },  // Darklord Eveningstar
            { 13331639, 7 },  // Supreme King Z-ARC
            { 13331640, 7 },  // Supreme King Z-ARC
            { 15419596, 7 },  // Ghost Bird of Bewitchment
            { 16259549, 7 },  // Number 49: Fortune Tune
            { 18386170, 7 },  // Dante, Pilgrim of the Burning Abyss
            { 20654247, 7 },  // Blue-Eyes Chaos Dragon
            { 20849090, 7 },  // Kozmo Forerunner
            { 23776077, 7 },  // Hazy Flame Basiltrice
            { 24550676, 7 },  // Lunalight Leo Dancer
            { 28400508, 7 },  // Number 97: Draglubion
            { 30698243, 7 },  // Red Hypernova Dragon
            { 31303283, 7 },  // Hazy Flame Hyppogrif
            { 34550857, 7 },  // Lyrilusc - Cobalt Sparrow
            { 34695290, 1 },  // Myutant Beast
            { 37803172, 7 },  // Hazy Flame Peryton
            { 38525760, 7 },  // Hazy Flame Cerbereus
            { 38811586, 7 },  // Albion the Incandescent Dragon
            { 43202238, 7 },  // Yazi, Evil of the Yang Zing
            { 45420955, 7 },  // Grozer the Thunderclap Tyrant
            { 46195773, 1 },  // Turbo Warrior
            { 49394035, 7 },  // Vendread Core
            { 52159691, 7 },  // Raiders' Wing
            { 55410871, 7 },  // Blue-Eyes Chaos MAX Dragon
            { 55410872, 7 },  // Blue-Eyes Chaos MAX Dragon
            { 55885348, 7 },  // Kozmo Dark Destroyer
            { 61089209, 2 },  // Myutant Mist
            { 61681816, 1 },  // Propa Gandake
            { 64063868, 7 },  // Kozmo Dark Eclipser
            { 69815951, 1 },  // Drytron Meteonis Draconids
            { 73121813, 7 },  // Tenyi Spirit - Mula Adhara
            { 74010769, 7 },  // Hazy Flame Griffin
            { 75191028, 7 },  // GMX - COMPREX
            { 82103466, 7 },  // Divine Serpent Geh
            { 85028288, 5 },  // Jurrac Titano
            { 85908279, 7 },  // Invoked Cocytus
            { 85991529, 7 },  // Kozmo Dark Planet
            { 88753594, 7 },  // Lunalight Sabre Dancer
            { 88754763, 7 },  // CXyz Coach Lord Ultimatrainer
            { 91025875, 7 },  // K9-ØØ Lupis
            { 94641726, 7 },  // Storm-Bane Dragon Destorbim
            { 95209656, 6 },  // Drytron Meteonis Quadrantids
            { 96051150, 7 },  // Hazy Flame Mantikor
            { 100000050, 7 },  // Meklord Emperor Wisel ∞ (TF6)
            { 100000056, 7 },  // Meklord Emperor Skiel ∞ (TF6)
            { 100000061, 7 },  // Meklord Emperor Granel ∞ (TF6)
            { 101201043, 1 },  // Evolzar Razh
            { 101403033, 1 },  // Triangle Ecstasy Harpie Lady Sisters
            { 110000010, 7 },  // The Winged Dragon of Ra (VG)
            { 110000011, 7 },  // Obelisk the Tormentor (VG)
            { 110000012, 7 },  // Slifer the Sky Dragon (VG)
            { 511600401, 7 },  // Meklord Emperor Wisel ∞ (TF5)
            { 511600409, 7 },  // Meklord Emperor Skiel ∞ (TF5)
            { 511600418, 7 },  // Meklord Emperor Granel ∞ (TF5)
            { 513000162, 7 },  // Zushin the Sleeping Giant (Anime)
        };
    }
}
