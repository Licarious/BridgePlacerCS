using BridgePlacerCS;
using CK3AutoIndexerCS;
using System.Drawing;
using System.Text;

internal class Program
{
    private static void Main(string[] args) {
        //localDir move 3 directories up
        string localDir = Directory.GetParent(Directory.GetParent(Directory.GetParent(Directory.GetCurrentDirectory()).ToString()).ToString()).ToString();
        
        bool regenerate = false;
        readConfig();

        //read the province map
        Bitmap map = new(localDir + @"\_Input\map_data\provinces.png");
        int mapHeight = map.Height;
        using StreamWriter debugFile = new(localDir + @"\_Output\debug.txt");

        if (regenerate) {
            List<float> debugAngles = new() { 0, 30, 45, 60, 90, 120, 135, 150 };
            //run debugAngles through DebugRotation
            foreach (float angle in debugAngles) {
                DebugRotation(angle);
            }
            debugFile.WriteLine();

            List<Province> provList = parseDefinition();
            GetCoords(provList);
            GetUnitLocator(provList);
            List<Adjacency> adjList = GetAdjacencies(provList, mapHeight);
            GetBridgeStyles(adjList);

            WriteBridges(adjList);
        }
        else {
            List<Adjacency> adjList = ParseBridgeLocators();
            GetBridgeStyles(adjList);
            WriteBridges(adjList);
        }
        debugFile.Close();

        //parse the definition.csv
        List<Province> parseDefinition() {
            List<Province> provinces = new();
            string[] lines = File.ReadAllLines(localDir + @"\_Input\map_data\definition.csv");
            foreach (string line in lines) {
                if (line.Trim() == "") continue;
                string[] split = line.Split(';');
                if (line.StartsWith("#")) {
                    continue;
                }

                //if first split is 0 continue
                if (split[0] == "0") continue;

                Province p = new(int.Parse(split[0]), int.Parse(split[1]), int.Parse(split[2]), int.Parse(split[3]), split[4]);
                provinces.Add(p);

            }       

            return provinces;

        }

        //read config settings
        void readConfig() {
            string[] config = File.ReadAllLines(localDir + @"\_Input\config.txt");

            foreach (string line in config) {
                if (line.Trim() == "" || line.Trim().StartsWith("#")) continue;

                if (line.Contains('=')) {
                    //key value
                    string[] split = line.Split('=');
                    if (split[0].Trim() == "regenerate")
                        regenerate = bool.Parse(split[1].Trim());
                }

            }

        }

        //read the province map
        int GetCoords(List<Province> provList) {
            //convert list to dictonary for faster lookup with color as key
            Dictionary<Color, Province> provDict = new();
            foreach (Province p in provList) {
                provDict.Add(p.color, p);
            }

            Console.WriteLine("Reading province map...");

            //loop through every pixel
            for (int x = 0; x < map.Width; x++) {
                for (int y = 0; y < map.Height; y++) {
                    //get the color of the pixel
                    Color c = map.GetPixel(x, y);
                    //if the color is in the dictionary
                    if (provDict.TryGetValue(c, out Province value)) {
                        value.coords.Add((x, y));
                    }
                }
                //print progress every 10% of the image
                if (x % (map.Width / 10) == 0) {
                    Console.WriteLine(x / (map.Width / 10) + "0%");
                }
            }

            //get the center of each province
            foreach (Province p in provList) {
                p.GetCenter();
            }

            return map.Height;

        }
    
        List<Adjacency> GetAdjacencies(List<Province> provList, int mapHeight) {
            //read each line in adjacencies.csv
            string[] lines = File.ReadAllLines(localDir + @"\_Input\map_data\adjacencies.csv");

            List<Adjacency> adjacenciesList = new();

            foreach (string line in lines) {
                if (line.Trim() == "" || line.Trim().StartsWith("#")) continue;

                //split the line
                string[] split = line.Split(';');
                //if the 3rd split is not "river_large" continue
                if (split[2] != "river_large") continue;

                //get the province ids
                int id1 = int.Parse(split[0]);
                int id2 = int.Parse(split[1]);

                //get the provinces
                Province p1 = provList.Find(p => p.id == id1);
                Province p2 = provList.Find(p => p.id == id2);

                //if either province is null continue
                if (p1 == null || p2 == null) {
                    //write the id of the province that is null
                    Console.WriteLine("\t\tProvince " + (p1 == null ? id1 : id2) + " not found");
                    continue;
                }

                //find the closest point in p1 to the center of p2 and vice versa using GetClosestPoint
                (float, float) p1ClosestCenter = GetClosestPoint(p1, p2.center);
                (float, float) p2ClosestCenter = GetClosestPoint(p2, p1.center);
                (float, float) p1ClosestCombat = GetClosestPoint(p1, p2.combatPosition);
                (float, float) p2ClosestCombat = GetClosestPoint(p2, p1.combatPosition);
                (float, float) p1ClosestPlayer = GetClosestPoint(p1, p2.playerStackPosition);
                (float, float) p2ClosestPlayer = GetClosestPoint(p2, p1.playerStackPosition);

                List<(float, float)> p1List = new() { p1ClosestCenter };
                List<(float, float)> p2List = new() { p2ClosestCenter };

                //add combat positions if both are not (-1,-1)
                if (p1.combatPosition != (-1, -1) && p2.combatPosition != (-1, -1)) {
                    p1List.Add(p1ClosestCombat);
                    p2List.Add(p2ClosestCombat);
                }
                //add player positions if both are not (-1,-1)
                if (p1.playerStackPosition != (-1, -1) && p2.playerStackPosition != (-1, -1)) {
                    p1List.Add(p1ClosestPlayer);
                    p2List.Add(p2ClosestPlayer);
                }
                
                //averate the two points to get the bridge location
                (float, float) bridgePositon = (0, 0);
                foreach ((float, float) p in p1List) {
                    bridgePositon.Item1 += p.Item1;
                    bridgePositon.Item2 += p.Item2;
                }
                bridgePositon.Item1 /= p1List.Count;
                bridgePositon.Item2 /= p1List.Count;


                //find bridgeRotation of the bridge using GetRotation
                (float, float) rotation = GetRotation2(bridgePositon, p1, p2);
                //get backup roation of the bridge using GetRotation if sweep failes
                if (rotation == (0, 0)) {
                    rotation = GetRotation(p1ClosestCenter, p2ClosestCenter);
                }

                //create the adjacency
                Adjacency adj = new(id1, id2, split[2], bridgePositon, rotation);
                adjacenciesList.Add(adj);

            }

            return adjacenciesList;
        }

        (int, int) GetClosestPoint(Province p, (float, float) point) {
            (int, int) closest = (0, 0);
            float closestDist = float.MaxValue;
            foreach ((int, int) coord in p.coords) {
                float dist = (float)Math.Sqrt(Math.Pow(coord.Item1 - point.Item1, 2) + Math.Pow(coord.Item2 - point.Item2, 2));
                if (dist < closestDist) {
                    closestDist = dist;
                    closest = coord;
                }
            }
            return (closest.Item1, mapHeight - closest.Item2);
        }

        (float, float) GetRotation((float, float) p1, (float, float) p2) {
            //0      N               0.075907 0.000000 -0.997115
            //30                    -0.273972 0.000000 -0.961738
            //45     NE             -0.445152 0.000000 -0.895456
            //60                    -0.509340 0.000000 -0.860566
            //90     E              -0.672494 0.000000 -0.740103
            //120                   -0.845019 0.000000 -0.534736
            //135    SE             -0.944954 0.000000 -0.327206
            //150                   -0.988875 0.000000 -0.148755
            //sin(angle/2) 0 cos(angle/2)


            //get the angle between the two points
            float angle = (float)Math.Atan2(p2.Item2 - p1.Item2, p2.Item1 - p1.Item1);
            //convert to degrees
            angle *= (180 / (float)Math.PI);
            //sin(angle/2) cos(angle/2)
            float sin = (float)-Math.Sin(angle * Math.PI / 180 / 2);
            float cos = (float)-Math.Cos(angle * Math.PI / 180 / 2);

            return (sin, cos);

        }

        (float, float) GetRotation2((float, float) center, Province p1, Province p2) {
            //convert center to int
            (int, int) centerInt = ((int)center.Item1, mapHeight - (int)center.Item2);


            List<float> angles = new();
            
            for (int i = 5; i< 30; i+=2) {
                float a = SweepCheck(i, centerInt, p1, p2);
                if (a != -720) {
                    angles.Add(a);
                    //Console.WriteLine("Sweep check success for " + p1.id + " , " + p2.id + " at " + i + " size");
                    if (angles.Count == 10) {
                        //Console.WriteLine("Sweep check success for " + p1.id + " , " + p2.id);
                        break;
                    }
                }
                
            }

            //if angle is still 0,0 try a sweep check of 3
            if (angles.Count == 0) {
                float a = SweepCheck(5, centerInt, p1, p2);
                if (a == -720) {
                    Console.WriteLine("\t\tcound not find angle for " + p1.id + " and " + p2.id + " at " + centerInt);
                    return (0, 0);
                }
            }

            //average elemenrts in angles to get the average angle
            float angle = 0;
            foreach (float a in angles) {
                angle += a;
            }
            angle /= angles.Count;
            //return sin and cos of the angle
            float sin = (float)-Math.Sin(angle * Math.PI / 180 / 2);
            float cos = (float)-Math.Cos(angle * Math.PI / 180 / 2);

            debugFile.WriteLine(p1.id + " " + ColorToHex(p1.color) + "\t\t" + p2.id + " " + ColorToHex(p2.color) + "\t\t" + angle + "° " + center);

            return (sin,cos);

        }

        //color to hexcode
        string ColorToHex(Color c) {
            return c.R.ToString("X2") + c.G.ToString("X2") + c.B.ToString("X2");
        }

        float SweepCheck(int sweepSize, (int, int) centerInt, Province p1, Province p2) {
            //loaer and uper bounds of the sweep
            int lower = (sweepSize / 2) - sweepSize +1;
            int upper = sweepSize / 2 +1;

            //Console.WriteLine("Sweeping " + lower + " to " + upper);

            //get the pixles in a sweepSize x sweepSize square around the center as a 2d array
            Color[,] colors = new Color[sweepSize, sweepSize];
            for (int x = lower; x < upper; x++) {
                for (int y = lower; y < upper; y++) {
                    colors[x - lower, y - lower] = map.GetPixel(centerInt.Item1 + x, centerInt.Item2 + y);
                }
            }
            

            bool[] arch = new bool[(2 * sweepSize) - 1];
            

            for (int x= 0; x<sweepSize; x++) {
                //top
                if (colors[x, 0] == p1.color && colors[sweepSize - x - 1, sweepSize - 1] == p2.color) {
                    arch[x] = true;
                }
                else if (colors[x, 0] == p2.color && colors[sweepSize - x - 1, sweepSize - 1] == p1.color) {
                    arch[x] = true;
                }
                //side
                if (colors[sweepSize - 1, x] == p1.color && colors[sweepSize - x - 1, 0] == p2.color) {
                    arch[sweepSize - 1 + x] = true;
                }
                else if (colors[sweepSize - 1, x] == p2.color && colors[sweepSize - x - 1, 0] == p1.color) {
                    arch[sweepSize - 1 + x] = true;
                }
            }

            

            //find the largest range of true values in arch and return the rotation
            int largestRange = 0;
            int largestRangeStart = 0;
            int currentRange = 0;
            int currentRangeStart = 0;
            for (int i = 0; i < arch.Length - 1; i++) {
                if (arch[i]) {
                    if (currentRange == 0) {
                        currentRangeStart = i;
                    }
                    currentRange++;
                }
                else {
                    if (currentRange > largestRange) {
                        largestRange = currentRange;
                        largestRangeStart = currentRangeStart;
                    }
                    currentRange = 0;
                }
            }

            //if the largest range is 0 return 0
            if (largestRange == 0) {
                return -720;
            }
            
            //get the average of the start and end of the largest range
            float angle = (largestRangeStart + largestRangeStart + largestRange) / 2;

            //knowing that 0 in the array is -45 degrees and last is 135 degrees convert the angle to degrees
            angle = (angle + lower) * (180/ (sweepSize - 1));
            
            return angle;
        }

        //find bridge styles
        void GetBridgeStyles(List<Adjacency> adjList) {
            Console.WriteLine("Parsing Styles");

            Dictionary<Color, Province> styleDict = new();

            //parse the bridge styles file
            string[] lines = File.ReadAllLines(localDir + @"\_Input\bridgeStyle.csv");
            Province defaultStyle = null;

            //loop through each line
            foreach(string line in lines) {
                if (line.Trim() == "" || line.Trim().StartsWith("#")) continue;

                //split the line
                string[] split = line.Split(';');

                //name, otherinfo, Red, Green, Blue
                //create province with the color
                Province p = new(-1, int.Parse(split[2]), int.Parse(split[3]), int.Parse(split[4]), split[0]) {
                    otherInfo = split[1]
                };

                //set default style
                defaultStyle ??= p;

                //add the province to the dictionary
                styleDict.Add(p.color, p);

            }

            //get coords of each prov in styleDict
            Bitmap map = new(localDir + @"\_Input\StyleMap.png");

            
            //loop through every pixel
            for (int x = 0; x < map.Width; x++) {
                for (int y = 0; y < map.Height; y++) {
                    //get the color of the pixel
                    Color c = map.GetPixel(x, y);
                    //if the color is in the dictionary
                    if (styleDict.TryGetValue(c, out var value)) {
                        value.coords.Add((x, map.Height-y));
                    }
                    
                }
                //print progress every 10% of the image
                if (x % (map.Width / 10) == 0) {
                    Console.WriteLine(x / (map.Width / 10) + "0%");
                }
            }
            
            //set styles on adj
            foreach(Adjacency adj in adjList) {
                //convert bridgePosition to int
                (int, int) tmpPos = ((int)adj.bridgePosition.Item1, (int)adj.bridgePosition.Item2);

                bool foundCoords = false;
                //check each element of styleDict to see if they contain the coord tmpPos
                foreach (KeyValuePair<Color, Province> kvp in styleDict) {
                    if (kvp.Value.coords.Contains(tmpPos)) {
                        adj.bridgeMesh = kvp.Value.otherInfo;
                        adj.bridgeName = kvp.Value.name;
                        foundCoords = true;
                        break;
                    }
                }

                //use default in case no style was found
                if (!foundCoords && defaultStyle != null) {
                    adj.bridgeMesh = defaultStyle.otherInfo;
                    adj.bridgeName = defaultStyle.name;
                }
                
            }
        }


        void WriteBridges(List<Adjacency> adjacenciesList) {
            //create the file with utf-8-BOM encoding
            using StreamWriter file = new(localDir + @"\_Output\bridges.txt", false, new UTF8Encoding(true));
            

            //group adj by bridgeMesh
            var groups = adjacenciesList.GroupBy(a => a.bridgeMesh);

            float verticalOffset = 1.0f;
            //loop through each group
            foreach (var group in groups) {
                //write header
                file.WriteLine("object={");
                file.WriteLine("\tname=\"" + group.First().bridgeName + "\"");
                file.WriteLine("\tclamp_to_water_level=no");
                file.WriteLine("\trender_under_water=yes"); 
                file.WriteLine("\tgenerated_content=no");
                file.WriteLine("\tlayer=\"temp_layer\"");
                file.WriteLine("\tpdxmesh=\"" + group.First().bridgeMesh + "\"");
                file.WriteLine("\tcount=" + group.Count());
                file.WriteLine("\ttransform=\"");

                //loop through each adjacency in the group
                foreach (Adjacency adj in group) {
                    if (adj.id1 == -1) {
                        file.WriteLine(adj.bridgeLine);
                    }
                    else {
                        //write position and bridgeRotation
                        file.WriteLine(adj.bridgePosition.Item1 + " " + verticalOffset + " " + adj.bridgePosition.Item2 + " 0.0 " + adj.bridgeRotation.Item1 + " 0.0 " + adj.bridgeRotation.Item2 + " 1.0 1.0 1.0");
                    }
                }

                file.WriteLine("\"}");
            }
            
            //close the file
            file.Close();

        }

        void GetUnitLocator(List<Province> provList) {
            string[] fileList = new string[] { @"\_Input\map_object_data\player_stack_locators.txt", @"\_Input\map_object_data\combat_locators.txt" };


            //loop through each file
            foreach (string file in fileList) {
                try {
                    //read the unit locator file
                    string[] lines = File.ReadAllLines(localDir + file);

                    int indentation = 0;
                    Province currentProvince = null;
                    //loop through each line
                    foreach (string line in lines) {
                        string l1 = CleanLine(line);
                        //if l1 is empty or starts with # skip it
                        if (l1 == "") continue;

                        //if l1 starts with id
                        if (l1.StartsWith("id")) {
                            //get the id
                            int id = int.Parse(l1.Split('=')[1]);
                            //get the province with the id
                            currentProvince = provList.Find(p => p.id == id);
                        }

                        //if l1 starts with position
                        if (l1.StartsWith("position")) {
                            //get the position
                            string[] split = l1.Split('{')[1].Split('}')[0].Trim().Split();
                            if (currentProvince != null) {
                                //set the position
                                //if file name contains "player_stack"
                                if (file.Contains("player_stack")) {
                                    currentProvince.playerStackPosition = (float.Parse(split[0]), (float)mapHeight - float.Parse(split[2]));
                                }
                                else if (file.Contains("combat")) {
                                    currentProvince.combatPosition = (float.Parse(split[0]), (float)mapHeight - float.Parse(split[2]));
                                }
                            }
                        }



                        //if l1 contains { or } change indentation
                        if (l1.Contains('{')) {
                            indentation++;
                        }
                        if (l1.Contains('}')) {
                            indentation--;
                            if (indentation == 2) {
                                currentProvince = null;
                            }
                        }
                    }
                }
                catch (Exception e) {
                    Console.WriteLine("Error reading file: " + file);
                    Console.WriteLine(e.Message);
                }
            }


        }

        //parse bridge locators
        List<Adjacency> ParseBridgeLocators() {
            string[] lines = File.ReadAllLines(localDir + @"\_Input\map_object_data\bridges.txt");

            string currentBridgeName = "";
            string currentBridgeMesh = "";
            bool bridgeStart = false;
            List<Adjacency> adjList = new();
            //loop through each line
            foreach (string line in lines) {
                if (line.Trim().StartsWith("name")) {
                    currentBridgeName = line.Split('=')[1].Replace("\"","");
                }
                else if (line.Trim().StartsWith("pdxmesh")) {
                    currentBridgeMesh = line.Split('=')[1].Replace("\"", "");
                }
                else if (line.Trim().StartsWith("transform")) {
                    bridgeStart = true;
                    
                }

                if (line.Contains('}')) {
                    bridgeStart = false;
                }

                if (bridgeStart) {
                    string bridgeline = "";
                    if (line.Contains('=')) bridgeline = line.Split('=')[1].Replace("\"", "");
                    else bridgeline = line;
                    Adjacency tmp = new(bridgeline) {
                        bridgeName = currentBridgeName,
                        bridgeMesh = currentBridgeMesh
                    };

                    adjList.Add(tmp);
                }
            }

            return adjList;

        }

        //clean line
        string CleanLine(string line) {
            return line.Trim().Replace("=", " = ").Replace("{", " { ").Replace("}", " } ").Replace("  ", " ").Trim().Split('#')[0];
        }

        //debug rotation function
        void DebugRotation(float angle) {

            debugFile.WriteLine(angle + "\t" + -Math.Sin(angle * Math.PI / 180 / 2) + "\t" + -Math.Cos(angle * Math.PI / 180 / 2));
        }
    }


}
