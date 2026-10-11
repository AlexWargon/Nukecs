using UnityEngine;

namespace Wargon.Nukecs
{
    public abstract class Convertor : ScriptableObject, ICustomConvertor {
        public abstract void Convert(ref World world, ref Entity entity);
    }
}
