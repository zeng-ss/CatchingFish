using System;
using System.Collections.Generic;
using UnityEngine;

namespace Config
{
    [CreateAssetMenu(fileName = "BgSizeConfig", menuName = "BgSize Config")]
    public class BgSizeConfig : ScriptableObject
    {
        public List<BgSize> bgSizes = new();
    }

    [Serializable]
    public class BgSize
    {
        public string name;
        [Header("横屏")] public float hScale;
        [Header("竖屏")] public float pScale;
    }
}