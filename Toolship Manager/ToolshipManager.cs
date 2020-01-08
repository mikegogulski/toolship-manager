using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;
using System;
using VRage.Collections;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Game;
using VRage;
using VRageMath;

namespace IngameScript
{
    partial class Program : MyGridProgram
    {
        // Copy from below the divider to before the last two } and paste into programmable block
        // --------------------------------------------------------------------------------------
        // SerotoninCraver's Toolship Manager v1.0 - https://github.org/mikegogulski/toolship-manager
        // Public domain under the Unlicense: http://unlicense.org/
        // Based on Better Tools V1.1 by Velenka (thanks, pal)

        // This script makes it easy to empty out grinder and mining ships and restock welding ships with appropriate components.
        //
        // If you use Isy's Inventory Manager on grids you connect to, you'll want to tag "[No IIM]" on the connector or tag
        // individual containers as "Locked".
        //
        // NOTE: No timer block is required, but you need to set up a toolbar with 8 copies of the programmable block, using
        // these arguments: up, down, left, right, increment, decrement, load, unload

        // Configuration
        // ---------------------------------------------------------------------------------------
        string displayName = "LCD SCTMMenu"; // LCD for component count display/editing.
        string debugName = "LCD SCTMDebug"; // LCD for debugging. Not necessary if useDebugLCD is false.
        bool useDebugLCD = true; // Use true if you want to see debug messages on an LCD.
        bool enableDebug = true; // Used for Visual Studio development. Also enables logging on the programmable block.

        // Relative amounts of components of each type to keep stocked in the ship. Do not delete entries, only edit values.
        Dictionary<string, float> stockRelative = new Dictionary<string, float>()
        {
            {"Missile200mm", 0},
            {"NATO_25x184mm", 0},
            {"BulletproofGlass", 100},
            {"Computer", 500},
            {"Construction", 2500},
            {"Detector", 10},
            {"Display", 200},
            {"Explosives", 0},
            {"Girder", 30},
            {"GravityGenerator", 10},
            {"InteriorPlate", 2000},
            {"LargeTube", 200},
            {"Medical", 10},
            {"MetalGrid", 100},
            {"Motor", 500},
            {"PowerCell", 50},
            {"RadioCommunication", 10},
            {"Reactor", 100},
            {"SmallTube", 500},
            {"SolarCell", 50},
            {"SteelPlate", 5000},
            {"Superconductor", 20},
            {"Thrust", 20},
            {"Canvas", 0 }
        };

        // Tag the programmable block's grid's container names with these strings for the script to ignore them
        List<string> myExceptions = new List<string>() { "SCTMIgnore" };
        // Tag other grids' container names with these strings for the script to ignore them
        List<string> theirExceptions = new List<string>() { "SCTMIgnore", "SCTMPersonal", "SCTMWhatever" };

        // Do not edit below this line (haha do whatever you want lol)
        //-------------------------------------------------------------------------------------------------------------------------------------------------------------------- 
        IMyTextPanel lcd;
        List<string> comps = new List<string>() { "Missile200mm", "NATO_25x184mm", "BulletproofGlass", "Computer", "Construction", "Detector", "Display", "Explosives", "Girder", "GravityGenerator", "InteriorPlate", "LargeTube", "Medical", "MetalGrid", "Motor", "PowerCell", "RadioCommunication", "Reactor", "SmallTube", "SolarCell", "SteelPlate", "Superconductor", "Thrust", "Canvas" };
        List<string> compNames = new List<string>() { "Missile200mm", "NATO_25x184mm", "BPGlass", "Computer", "Construction", "Detector", "Display", "Explosives", "Girder", "GravGen", "InteriorPlate", "LargeTube", "Medical", "MetalGrid", "Motor", "PowerCell", "RadioComm", "Reactor", "SmallTube", "SolarCell", "SteelPlate", "Supercond", "Thrust", "Canvas" };

        // Manifest to track where everything is located within the system
        Dictionary<string, Dictionary<int, IMyTerminalBlock>> compManifest = new Dictionary<string, Dictionary<int, IMyTerminalBlock>>();
        // Running totals of each component
        Dictionary<string, int> compTotals = new Dictionary<string, int>();
        // Volumes of each component
        Dictionary<string, int> compVol = new Dictionary<string, int>()
        {
            {"Missile200mm", 16},
            {"NATO_25x184mm", 0},
            {"BulletproofGlass", 8},
            {"Computer", 1},
            {"Construction", 2},
            {"Detector", 6},
            {"Display", 6},
            {"Explosives", 2},
            {"Girder", 2},
            {"GravityGenerator", 200},
            {"InteriorPlate", 5},
            {"LargeTube", 38},
            {"Medical", 160},
            {"MetalGrid", 15},
            {"Motor", 8},
            {"PowerCell", 40},
            {"RadioCommunication", 70},
            {"Reactor", 8},
            {"SmallTube", 2},
            {"SolarCell", 20},
            {"SteelPlate", 3},
            {"Superconductor", 8},
            {"Thrust", 10},
            {"Canvas", 8 }
        };

        List<IMyTerminalBlock> myInv = new List<IMyTerminalBlock>();
        List<IMyTerminalBlock> theirInv = new List<IMyTerminalBlock>();
        Dictionary<char, byte> charWidths = new Dictionary<char, byte>();
        int menuPos = 0;
        float delta = 1f;
        List<string> menuCommands = new List<string>() { "up", "down", "left", "right", "increment", "decrement" };

        public Program()
        {
            if (Storage.Length > 0)
            {
                stockRelative = Storage.Split(';').Select(x => x.Split(',')).ToDictionary(y => y[0], y => float.Parse(y[1]));
            }
            DefineCharWidths();
        }

        public void Save()
        {
            Storage = string.Join(";", stockRelative.Select(kvp => kvp.Key + ',' + kvp.Value.ToString()));
        }

        public void Main(string argument)
        {
            try
            {
                SetupPrint(debugName);
                Print("Begin");
                GetStock();
                if (menuCommands.Contains(argument))
                {
                    if (Menu(argument))
                        return;
                }

                Print("Initializing");
                if (!Initialize(argument))
                    return;
                Print("Initialized\n");

                if (argument.Equals("load"))
                {
                    Print("begin moving into");
                    MoveItemsInto();
                    Print("success moving into");
                }
                Print("Script complete");
            } catch (Exception e)
            {
                Echo($"Exception: {e}\n---");
                Print($"Exception: {e}\n---"); // woe unto us if the exception was in Print()!
                throw;
            }
        }

        bool Initialize(string argument)
        {
            if (comps.Count == 0)
            {
                foreach (string c in comps)
                {
                    compManifest.Add(c, new Dictionary<int, IMyTerminalBlock>());
                    compTotals.Add(c, 0);
                }
            }
            GetInv();

            if (myInv.Count == 0)
            {
                Print("No local inventories");
                return false;
            }
            if (theirInv.Count == 0)
            {
                Print("No external inventories");
                return false;
            }

            Print("begin moving stuff out");
            if (!MoveItemsOut())
                return false;
            Print("success moving stuff out");

            if (argument.Equals("load"))
            {
                Print("begin moving stuff in");
                GetCompManifest();
                Print("success moving stuff in");
            }
            return true;
        }

        void RemoveExceptions(ref List<IMyTerminalBlock> inv, List<string> exceptions)
        {
            foreach (string e in exceptions)
            {
                inv.RemoveAll((x) => x.CustomName.Contains(e));
            }
        }

        void GetInv()
        {
            var tempList = new List<IMyTerminalBlock>();
            myInv.Clear();
            theirInv.Clear();
            GridTerminalSystem.GetBlocksOfType<IMyCargoContainer>(tempList, a => { return a.CubeGrid == Me.CubeGrid; });
            myInv.AddRange(tempList);
            GridTerminalSystem.GetBlocksOfType<IMyShipDrill>(tempList, a => { return a.CubeGrid == Me.CubeGrid; });
            myInv.AddRange(tempList);
            GridTerminalSystem.GetBlocksOfType<IMyShipWelder>(tempList, a => { return a.CubeGrid == Me.CubeGrid; });
            myInv.AddRange(tempList);
            GridTerminalSystem.GetBlocksOfType<IMyShipGrinder>(tempList, a => { return a.CubeGrid == Me.CubeGrid; });
            myInv.AddRange(tempList);
            List<IMyTerminalBlock> bogolist = new List<IMyTerminalBlock>();
            GridTerminalSystem.GetBlocksOfType<IMyShipConnector>(tempList, a => { return a.CubeGrid == Me.CubeGrid; });
            tempList.RemoveAll((x) => x.DefinitionDisplayNameText.Equals("Ejector"));
            myInv.AddRange(tempList);
            GridTerminalSystem.GetBlocksOfType<IMyCargoContainer>(theirInv, a => { return a.CubeGrid != Me.CubeGrid; });
            RemoveExceptions(ref myInv, myExceptions);
            Print("MyInv " + myInv.Count);
            RemoveExceptions(ref theirInv, theirExceptions);
            Print("TheirInv " + theirInv.Count);
        }

        bool MoveItemsOut()
        {
            for (int i = 0; i < myInv.Count; i++)
            {
                var inv0 = myInv[i].GetInventory(0);
                List<MyInventoryItem> items = new List<MyInventoryItem>();
                inv0.GetItems(items);
                int contIndex = 0;
                var contInv = theirInv[contIndex].GetInventory(0);

                for (int j = 0; j < items.Count; j++)
                {
                    // check if receiving container is full
                    while ((float)contInv.CurrentVolume / (float)contInv.MaxVolume > 0.975f)
                    {
                        // Print(theirInv[contIndex].CustomName); 
                        contIndex++;
                        if (contIndex >= theirInv.Count) // then the entire system is full 
                        {
                            Print("Containers are all full!\nStopping Script");
                            return false;
                        }
                        contInv = theirInv[contIndex].GetInventory(0);
                    }

                    // transfer each item out
                    bool wtf = inv0.TransferItemTo(contInv, 0, stackIfPossible: true);

                    if ((float)contInv.CurrentVolume / (float)contInv.MaxVolume > 0.975f)
                    {
                        j--;
                    }
                }
            }
            return true;
        }

        void MoveItemsInto()
        {
            float totalRelVol = 0f;

            for (int k = 0; k < comps.Count; k++)
            {
                if (compTotals[comps[k]] == 0)
                    continue;
                totalRelVol += stockRelative[comps[k]] * compVol[comps[k]];
            }
            Print("total relative volume " + totalRelVol);

            float totalVol = myInv.Sum((x) => (float)x.GetInventory(0).MaxVolume * 1000f);
            Print("total volume " + totalVol);
            /*
            Print(String.Join(
                "\n", myInv.Select(
                    (x) => 
                    x.CustomName + 
                    " maxvol: " + 
                    (float)x.GetInventory(0).MaxVolume * 1000f + 
                    " curvol: " + 
                    x.GetInventory(0).CurrentVolume * 1000f)));
            */


            float multiplier = totalVol / totalRelVol;
            Print("multiplier " + multiplier);

            for (int k = 0; k < comps.Count; k++)
            {
                if (compTotals[comps[k]] == 0 || stockRelative[comps[k]] <= 0)
                    continue;
                // get manifest of current items 
                var myManifest = compManifest[comps[k]];
                float myAmtTotal = multiplier * stockRelative[comps[k]];
                if (myAmtTotal < 1f)
                    continue;
                Print("need " + myAmtTotal + " " + comps[k] + "s");

                int manifestIndex = 0;

                foreach (IMyTerminalBlock i in myInv)
                {
                    Print("Filling " + i.CustomName);
                    // Print("frac of tot " + ((float)i.GetInventory(0).MaxVolume * 1000f / totalVol));
                    Print(((float)i.GetInventory(0).MaxVolume * 1000f / totalVol).ToString("0.0") + "% of total");
                    int myAmtEach = (int)(myAmtTotal * (float)i.GetInventory(0).MaxVolume * 1000f / totalVol);
                    Print("Need " + myAmtEach + " " + comps[k] + "s");
                    int currentTotal = 0;

                    // keep going until myAmtEach is in this container
                    while (currentTotal < myAmtEach - 1)
                    {
                        Print("    " + currentTotal + " of " + myAmtEach);
                        Print("    item group " + manifestIndex);

                        var inv = myManifest[manifestIndex].GetInventory(0);
                        List<MyInventoryItem> items = new List<MyInventoryItem>();
                        inv.GetItems(items);
                        // find item in the container 
                        foreach (MyInventoryItem j in items)
                        {
                            string name = j.Type.SubtypeId;
                            if (name == comps[k] && getCompName(name, j.Type.TypeId.ToString()))
                            {
                                // amount at this location is thisAmt
                                int thisAmt = (int)j.Amount;
                                Print("Found " + thisAmt + " " + name + "s");
                                inv.TransferItemTo(i.GetInventory(0), j, amount: (myAmtEach - currentTotal));

                                if ((myAmtEach - currentTotal) >= thisAmt) // amount completely removed, remove from manifest
                                {
                                    bool next = false;
                                    Print(manifestIndex + " of " + (myManifest.Count - 1));
                                    if (manifestIndex == myManifest.Count - 1)
                                        next = true;
                                    else // we still need more so we move on to the next location
                                        next = myManifest[manifestIndex] != myManifest[manifestIndex + 1];

                                    manifestIndex++;
                                    currentTotal += thisAmt;
                                    if (next)
                                        break;
                                } else
                                {
                                    currentTotal = myAmtEach;
                                    break;
                                }
                            }
                        }
                    }
                }
            }
            return;
        }

        bool getCompName(string name, string type)
        {
            return type.EndsWith("_Component") || (type.EndsWith("_AmmoMagazine") && name != "NATO_5p56x45mm");
        }

        void GetCompManifest()
        {
            compManifest = new Dictionary<string, Dictionary<int, IMyTerminalBlock>>();
            compTotals = new Dictionary<string, int>();
            var manifestIndex = new Dictionary<string, int>();
            foreach (string c in comps)
            {
                compManifest.Add(c, new Dictionary<int, IMyTerminalBlock>());
                compTotals.Add(c, 0);
                manifestIndex.Add(c, 0);
            }

            for (int i = 0; i < theirInv.Count; i++)
            {
                var thisInv = theirInv[i].GetInventory(0);
                List<MyInventoryItem> items = new List<MyInventoryItem>();
                thisInv.GetItems(items);

                for (int j = 0; j < items.Count; j++)
                {
                    string name = items[j].Type.SubtypeId;
                    if (getCompName(name, items[j].Type.TypeId.ToString()))
                    {
                        compManifest[name].Add(manifestIndex[name], theirInv[i]);
                        manifestIndex[name]++;
                        compTotals[name] += (int)items[j].Amount;
                    }
                }
            }
        }

        void SetupPrint(string name)
        {
            if (useDebugLCD && Me != null)
            {
                lcd = GridTerminalSystem.GetBlockWithName(name) as IMyTextPanel;
                if (lcd == null)
                {
                    Echo("No debug LCD found");
                    useDebugLCD = false;
                } else
                {
                    lcd.Alignment = TextAlignment.LEFT;
                    lcd.FontSize = 1;
                    lcd.ContentType = ContentType.TEXT_AND_IMAGE;
                    lcd.WriteText("", false);
                }
            }
        }

        void Print(string msg, bool newline = true)
        {
            if (enableDebug)
            {
                if (useDebugLCD && lcd != null)
                {
                    lcd.WriteText(msg + (newline ? "\n" : ""), true);
                }
                Echo(msg);
            }
        }

        string FormatNumber(float num)
        {
            // returns the number in SI suffix format
            num = (float)Math.Min(Math.Max(0, num), 99999999999999999999f);
            if (num == 0)
                return "0";

            var suffix = new Dictionary<int, string> {
                { 0, "" },
                { 3, "k" },
                { 6, "M" },
                { 9, "G" },
                { 12, "T" },
                { 15, "P" },
                { 18, "E" },
                { -3, "m" },
                { -6, "u" },
                { -9, "n" }
            };

            int exp = (int)Math.Floor(Math.Log10(num));
            int a = exp;
            while (a < 0)
                a += 3;

            exp = exp - (a % 3);

            float newNum = (float)Math.Round(num / Math.Pow(10, exp), 2);
            return newNum + suffix[exp];
        }

        List<string> Spacing(List<string> lines, int spaces)
        {
            var lineLengths = new List<int>(lines.Count);
            int maxLength = 0;

            for (int i = 0; i < lines.Count; i++)
            {
                int length = 0;
                for (int j = 0; j < lines[i].Length; j++)
                    length += charWidths[lines[i][j]];
                if (length > maxLength)
                    maxLength = length;
                lineLengths.Add(length);
            }

            int minFinalLength = maxLength + spaces * 9;
            // Print("minFL "+minFinalLength); 

            var newLines = new List<string>(lines);

            for (int i = 0; i < lines.Count; i++)
            {
                // Print("calc "+i);
                int diff = minFinalLength - lineLengths[i];
                // Print("diff "+diff); 
                int n11 = (int)Math.Ceiling(diff / 11.0);
                int n9 = diff / 9;

                float y9 = (diff - 9 * n9) / 2f;
                float y11 = (diff - 9 * n11) / 2f;
                // Print((y11<0)+" "+(y9>n9)); 

                if ((y11 < 0 && y9 > n9))
                {
                    //Print("reset");
                    minFinalLength++;
                    i = -1;
                    newLines = new List<string>(lines);
                    continue;
                }
                // Print((y9%1!=0) +" "+( y11%1!=0)); 

                if (y9 % 1 != 0 && y11 % 1 != 0)
                {
                    // Print("sub one "+i); 
                    n11 = (int)Math.Ceiling((diff - 1) / 11.0);
                    y11 = (diff - 1 - 9 * n11) / 2f;
                    float x11 = n11 - y11;
                    newLines[i] = lines[i];
                    for (int j = 0; j < x11; j++)
                        newLines[i] += ' ';
                    for (int j = 0; j < y11; j++)
                        newLines[i] += '\u00AD';
                    // Print("  adding " + (9 * x11 + 11 * y11));
                } else if (y11 % 1 != 0)
                {
                    // Print("use 9 "+i); 
                    float x9 = n9 - y9;
                    newLines[i] = lines[i];
                    for (int j = 0; j < x9; j++)
                        newLines[i] += ' ';
                    for (int j = 0; j < y9; j++)
                        newLines[i] += '\u00AD';
                    // Print("  adding " + (9 * x9 + 11 * y9));
                } else
                {
                    // Print("use 11 "+i); 
                    float x11 = n11 - y11;
                    newLines[i] = lines[i];
                    for (int j = 0; j < x11; j++)
                        newLines[i] += ' ';
                    for (int j = 0; j < y11; j++)
                        newLines[i] += '\u00AD';
                    // Print("  adding " + (9 * x11 + 11 * y11));
                }
                // else 
                // { 
                // Print("n9 "+n9); 
                // Print("n11 "+n11); 
                // Print("y9 "+y9); 
                // Print("y11 "+y11); 
                // } 
                // Print("finish "+i); 
            }
            return newLines;
        }

        void SetCharWidths(byte l, string s)
        {
            for (int i = 0; i < s.Length; i++)
                charWidths[s[i]] = l;
        }

        void DefineCharWidths()
        {
            SetCharWidths(0, "\u2028\u2029\u202F");
            SetCharWidths(7, "'|\u00A6\u02C9\u2018\u2019\u201A");
            SetCharWidths(8, "\u0458");
            SetCharWidths(9, " !I`ijl\u00A0\u00A1\u00A8\u00AF\u00B4\u00B8\u00CC\u00CD\u00CE\u00CF\u00EC\u00ED\u00EE\u00EF\u0128\u0129\u012A\u012B\u012E\u012F\u0130\u0131\u0135\u013A\u013C\u013E\u0142\u02C6\u02C7\u02D8\u02D9\u02DA\u02DB\u02DC\u02DD\u0406\u0407\u0456\u0457\u2039\u203A\u2219");
            SetCharWidths(10, "(),.1:;[]ft{}\u00B7\u0163\u0165\u0167\u021B");
            SetCharWidths(11, "\"-r\u00AA\u00AD\u00BA\u0140\u0155\u0157\u0159");//111111 
            SetCharWidths(12, "*\u00B2\u00B3\u00B9");
            SetCharWidths(13, "\\\u00B0\u201C\u201D\u201E");
            SetCharWidths(14, "\u0491");
            SetCharWidths(15, "/\u0133\u0442\u044D\u0454");
            SetCharWidths(16, "L_vx\u00AB\u00BB\u0139\u013B\u013D\u013F\u0141\u0413\u0433\u0437\u043B\u0445\u0447\u0490\u2013\u2022");
            SetCharWidths(17, "7?Jcz\u00A2\u00BF\u00E7\u0107\u0109\u010B\u010D\u0134\u017A\u017C\u017E\u0403\u0408\u0427\u0430\u0432\u0438\u0439\u043D\u043E\u043F\u0441\u044A\u044C\u0453\u0455\u045C");
            SetCharWidths(18, "3FKTabdeghknopqsuy\u00A3\u00B5\u00DD\u00E0\u00E1\u00E2\u00E3\u00E4\u00E5\u00E8\u00E9\u00EA\u00EB\u00F0\u00F1\u00F2\u00F3\u00F4\u00F5\u00F6\u00F8\u00F9\u00FA\u00FB\u00FC\u00FD\u00FE\u00FF\u00FF\u0101\u0103\u0105\u010F\u0111\u0113\u0115\u0117\u0119\u011B\u011D\u011F\u0121\u0123\u0125\u0127\u0136\u0137\u0144\u0146\u0148\u0149\u014D\u014F\u0151\u015B\u015D\u015F\u0161\u0162\u0164\u0166\u0169\u016B\u016D\u016F\u0171\u0173\u0176\u0177\u0178\u0219\u021A\u040E\u0417\u041A\u041B\u0431\u0434\u0435\u043A\u0440\u0443\u0446\u044F\u0451\u0452\u045B\u045E\u045F");
            SetCharWidths(19, "+<=>E^~\u00AC\u00B1\u00B6\u00C8\u00C9\u00CA\u00CB\u00D7\u00F7\u0112\u0114\u0116\u0118\u011A\u0404\u040F\u0415\u041D\u042D\u2212");
            SetCharWidths(20, "#0245689CXZ\u00A4\u00A5\u00C7\u00DF\u0106\u0108\u010A\u010C\u0179\u017B\u017D\u0192\u0401\u040C\u0410\u0411\u0412\u0414\u0418\u0419\u041F\u0420\u0421\u0422\u0423\u0425\u042C\u20AC");
            SetCharWidths(21, "$&GHPUVY\u00A7\u00D9\u00DA\u00DB\u00DC\u00DE\u0100\u011C\u011E\u0120\u0122\u0124\u0126\u0168\u016A\u016C\u016E\u0170\u0172\u041E\u0424\u0426\u042A\u042F\u0436\u044B\u2020\u2021");
            SetCharWidths(22, "ABDNOQRS\u00C0\u00C1\u00C2\u00C3\u00C4\u00C5\u00D0\u00D1\u00D2\u00D3\u00D4\u00D5\u00D6\u00D8\u0102\u0104\u010E\u0110\u0143\u0145\u0147\u014C\u014E\u0150\u0154\u0156\u0158\u015A\u015C\u015E\u0160\u0218\u0405\u040A\u0416\u0444");
            SetCharWidths(23, "\u0459");
            SetCharWidths(24, "\u044E");
            SetCharWidths(25, "%\u0132\u042B");
            SetCharWidths(26, "@\u00A9\u00AE\u043C\u0448\u045A");
            SetCharWidths(27, "M\u041C\u0428");
            SetCharWidths(28, "mw\u00BC\u0175\u042E\u0449");
            SetCharWidths(29, "\u00BE\u00E6\u0153\u0409");
            SetCharWidths(30, "\u00BD\u0429");
            SetCharWidths(31, "\u2122");
            SetCharWidths(32, "W\u00C6\u0152\u0174\u2014\u2026\u2030");
        }

        bool Menu(string arg)
        {
            Print("setting screen");
            var disp = GridTerminalSystem.GetBlockWithName(displayName) as IMyTextPanel;
            if (disp == null)
            {
                Print("Display LCD not found");
                return true;
            }
            float size = 1.2f;
            disp.SetValueFloat("FontSize", size);

            bool changeVal = false;
            int changeDir = 1;

            Print("Init stuff");
            int num = 15;
            double exp;
            var menuLines = new List<string>();
            for (int i = 0; i < comps.Count; i++)
                menuLines.Add("");
            float maxComp = 0f;

            Print("Parse args");
            switch (arg)
            {
            case "down":
                Print("menupos was " + menuPos);
                if (menuPos == menuLines.Count - 1)
                    menuPos = 0;
                else
                    menuPos++;
                Print("menupos is " + menuPos);
                break;
            case "up":
                Print("menupos was " + menuPos);
                if (menuPos == 0)
                    menuPos = menuLines.Count - 1;
                else
                    menuPos--;
                Print("menupos is " + menuPos);
                break;
            case "right":
                changeVal = true;
                changeDir = 1;
                break;
            case "left":
                changeVal = true;
                changeDir = -1;
                break;
            case "increment":
                exp = Math.Round(Math.Log10(delta));
                if (exp >= 9)
                    exp = 9;
                else
                    exp++;
                delta = (float)Math.Pow(10, exp);
                break;
            case "decrement":
                exp = Math.Round(Math.Log10(delta));
                if (exp <= 0)
                    exp = 0;
                else
                    exp--;
                delta = (float)Math.Pow(10, exp);
                break;
            }

            // Print("begin spacing");
            menuLines[menuPos] = ">";
            menuLines = Spacing(menuLines, 2);
            // Print("done first Spacing");

            for (int i = 0; i < comps.Count; i++)
                menuLines[i] += (compNames[i]);

            menuLines = Spacing(menuLines, 2);
            // Print("done second spacing");

            for (int i = 0; i < comps.Count; i++)
            {
                if (menuPos == i && changeVal)
                    stockRelative[comps[i]] = Math.Min(Math.Max(stockRelative[comps[i]] + delta * changeDir, 0), 90000000000000000000f);
                menuLines[i] += FormatNumber(stockRelative[comps[i]]);
            }
            StoreStock();

            menuLines = Spacing(menuLines, 2);
            // Print("Done third spacing");
            for (int i = 0; i < comps.Count; i++)
            {
                if (stockRelative[comps[i]] > maxComp)
                    maxComp = stockRelative[comps[i]];
            }

            for (int i = 0; i < comps.Count; i++)
            {
                menuLines[i] += "[";
                float frac = stockRelative[comps[i]] / maxComp;
                int n = (int)(frac * num);
                // Print(" frac" + frac);
                // Print(" n" + n);
                for (int j = 0; j < Math.Min(Math.Max(n, 0), num); j++)
                    menuLines[i] += "|";
                for (int j = 0; j < Math.Min(Math.Max(0, num - n), num); j++)
                    menuLines[i] += "'";
                menuLines[i] += "]";
            }

            Print("choosing writeLines");
            var writeLines = new List<string>();
            writeLines.Add("Add/Subtract " + FormatNumber(delta));

            int dispLines = (int)(18f / size) - 1;
            if (dispLines > menuLines.Count)
                dispLines = menuLines.Count;
            for (int i = 0; i < dispLines; i++)
            {
                int index = menuPos - dispLines / 2 + i;
                if (index < 0)
                    index += menuLines.Count;
                if (index > menuLines.Count - 1)
                    index -= menuLines.Count;
                writeLines.Add(menuLines[index]);
            }

            Print("printing");
            disp.WriteText("");
            for (int i = 0; i < writeLines.Count; i++)
                disp.WriteText(writeLines[i] + "\n", true);
            return true;
        }

        void StoreStock()
        {
            var s = new List<string>();
            for (int i = 0; i < comps.Count; i++)
                s.Add(stockRelative[comps[i]].ToString());
            Storage = String.Join(",", s);
        }

        void GetStock()
        {
            var s = new List<string>(Storage.Split(','));
            if (s.Count != comps.Count)
                return;
            for (int i = 0; i < comps.Count; i++)
            {
                float f = 0f;
                if (!Single.TryParse(s[i], out f))
                {
                    Print("Failed to get stockRelative for " + comps[i]);
                    return;
                }
                stockRelative[comps[i]] = f;
            }
        }
    }
}
