using System.Collections.Generic;
using UnityEngine;

namespace Dropecho {
  [CreateAssetMenu(menuName = "Dropecho/Snap Placer Preset")]
  public class SnapPlacerPreset : ScriptableObject {
    public float radius = 1;
    public int spawnCount = 1;
    [Range(0, 1)]
    public float alignToSurface = 0;
    public float minimumSpaceBetween = 1;
    public Material previewMaterial;
    public bool randomizeRotation = false;

    public List<GameObject> prefabs = new List<GameObject>();
  }
}
