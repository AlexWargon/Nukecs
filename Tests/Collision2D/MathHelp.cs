namespace Wargon.Nukecs.Collision2D
{
    public static class MathHelp {
        private static int floor(float x) {
            var xi = (int)x;
            return x < xi ? xi - 1 : xi;
        }

        public static int floorToInt(this float x) {
            var xint = (int)x;
            return x < xint ? xint - 1 : xint;
        }
    }
}  