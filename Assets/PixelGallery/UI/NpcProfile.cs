using UnityEngine;

namespace PixelGallery
{
    [CreateAssetMenu(menuName = "Pixel Gallery/NPC Taste")]
    public sealed class NpcProfile : ScriptableObject
    {
        public NpcTaste Taste;
    }
}
