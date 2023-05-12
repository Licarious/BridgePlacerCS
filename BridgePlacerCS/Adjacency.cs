using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BridgePlacerCS
{
    internal class Adjacency
    {

        public int id1;
        public int id2;
        public string type;
        public (float, float) bridgePosition;
        public (float, float) bridgeRotation;
        
        public string bridgeMesh;
        public string bridgeName;
        public string bridgeLine;

        public Adjacency(int id1, int id2, string type, (float, float) bridgePosition, (float, float) rotation) {
            this.id1 = id1;
            this.id2 = id2;
            this.type = type;
            this.bridgePosition = bridgePosition;
            this.bridgeRotation = rotation;
        }
        
        public Adjacency (string bridgeLine) {
            id1 = -1;
            id2 = -1;
            this.bridgeLine = bridgeLine;

            //position is 1st and 3rd number
            string[] split = bridgeLine.Split(' ');
            bridgePosition = (float.Parse(split[0]), float.Parse(split[2]));
        }





    }
}
