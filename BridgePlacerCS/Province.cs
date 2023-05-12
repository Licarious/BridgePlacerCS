using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK3AutoIndexerCS
{
    internal class Province
    {
        public int id = -1;
        public string name = "";
        public string otherInfo = "";
        public Color color;
        public HashSet<(int, int)> coords = new();
        public (float, float) center = (-1, -1);
        public (float, float) combatPosition = (-1, -1);
        public (float, float) playerStackPosition = (-1, -1);

        public Province(int id, int r, int g, int b, string name) { 
            this.id = id;
            this.name = name;
            this.color = Color.FromArgb(r, g, b);
            //this.otherInfo = otherInfo;
        }

        public void GetCenter() {
            if (coords.Count == 0) {
                return;
            }
            float x = 0;
            float y = 0;
            foreach ((int, int) coord in coords) {
                x += coord.Item1;
                y += coord.Item2;
            }
            x /= coords.Count;
            y /= coords.Count;
            center = (x, y);
        }

        
    }
}
