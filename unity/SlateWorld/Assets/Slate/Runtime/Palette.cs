// SLATE — the committed visual world, translated from the atlas palette into
// natural 3D tones. One coherent look: warm painterly realism at distance.
using UnityEngine;
using Slate.Sim;

namespace Slate.Game
{
    public static class Palette
    {
        // Water
        public static readonly Color DeepWater = new Color(0.10f, 0.22f, 0.26f);
        public static readonly Color ShallowWater = new Color(0.38f, 0.56f, 0.53f); // celadon, from the atlas
        public static readonly Color SeaFloorDeep = new Color(0.13f, 0.20f, 0.22f);
        public static readonly Color SeaFloorShallow = new Color(0.55f, 0.55f, 0.42f); // sandy shelf

        // Land by biome
        public static readonly Color Plains = new Color(0.435f, 0.510f, 0.270f);
        public static readonly Color Forest = new Color(0.290f, 0.400f, 0.215f);
        public static readonly Color Hills = new Color(0.485f, 0.460f, 0.290f);
        public static readonly Color Mountain = new Color(0.430f, 0.400f, 0.360f);
        public static readonly Color Desert = new Color(0.760f, 0.660f, 0.430f);
        public static readonly Color Snow = new Color(0.880f, 0.895f, 0.930f);
        public static readonly Color Marsh = new Color(0.330f, 0.420f, 0.300f);
        public static readonly Color SnowCap = new Color(0.930f, 0.935f, 0.955f);
        public static readonly Color RockSlope = new Color(0.470f, 0.430f, 0.380f);
        public static readonly Color RiverTint = new Color(0.280f, 0.420f, 0.400f);
        public static readonly Color RoadDirt = new Color(0.520f, 0.440f, 0.330f);

        // Buildings
        public static readonly Color WallStone = new Color(0.560f, 0.530f, 0.480f);
        public static readonly Color RuinChar = new Color(0.230f, 0.210f, 0.190f);
        public static readonly Color PlasterWarm = new Color(0.780f, 0.720f, 0.610f);
        public static readonly Color TrunkBrown = new Color(0.360f, 0.280f, 0.200f);
        public static readonly Color Canopy = new Color(0.250f, 0.370f, 0.190f);
        public static readonly Color CanopyDry = new Color(0.420f, 0.430f, 0.220f);

        // Divine / doom accents (from the atlas: gold #b08a2e, blood #8a3b2a)
        public static readonly Color DivineGold = new Color(0.690f, 0.541f, 0.180f);
        public static readonly Color DoomBlood = new Color(0.541f, 0.231f, 0.165f);
        public static readonly Color StormGray = new Color(0.180f, 0.200f, 0.260f);

        public static Color CultureColor(int culture)
        {
            // Culture colors from the design (names.js), slightly saturated for roofs.
            switch (culture)
            {
                case 0: return new Color(0.373f, 0.447f, 0.259f); // Aldish moss green
                case 1: return new Color(0.290f, 0.384f, 0.455f); // Vasker sea blue
                case 2: return new Color(0.635f, 0.396f, 0.184f); // Serai ochre
                case 3: return new Color(0.431f, 0.290f, 0.369f); // Tessian plum
                default: return Color.gray;
            }
        }

        public static Color BiomeColor(byte biome)
        {
            switch (biome)
            {
                case B.PLAINS: return Plains;
                case B.FOREST: return Forest;
                case B.HILLS: return Hills;
                case B.MOUNTAIN: return Mountain;
                case B.DESERT: return Desert;
                case B.SNOW: return Snow;
                case B.MARSH: return Marsh;
                case B.SHALLOW: return SeaFloorShallow;
                default: return SeaFloorDeep;
            }
        }
    }
}
