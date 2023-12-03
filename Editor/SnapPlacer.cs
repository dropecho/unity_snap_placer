using System.Linq;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;
using UnityEngine.UIElements;

namespace Dropecho {
  public class SnapPlacer : EditorWindow {
    public SnapPlacerPreset preset;
    private Vector2[] _randomPoints = new Vector2[] { };
    private int _spawnCount;

    [MenuItem("Tools/Dropecho/Snap Placer")]
    static void Open() {
      EditorWindow.GetWindow<SnapPlacer>("Snap Placer").Show();
    }

    void OnEnable() => SceneView.duringSceneGui += DuringSceneGUI;
    void OnDisable() => SceneView.duringSceneGui -= DuringSceneGUI;

    void CreateGUI() {
      var presets = AssetDatabaseUtils.GetAssetsOfType<SnapPlacerPreset>();
      var presetNames = presets.Select(x => x.name).ToList();
      preset = presets.Length > 0 ? presets[0] : null;

      DropdownField dropdown = null;
      if (preset) {
        dropdown = new DropdownField(presetNames, 0);
        dropdown.style.flexGrow = 1;
        dropdown.RegisterValueChangedCallback((evt) => {
          preset = presets.First(x => x.name == evt.newValue);

          rootVisualElement.Q("editor-container")?.RemoveFromHierarchy();
          if (preset) {
            rootVisualElement.Add(createPresetEditor());
            _spawnCount = preset?.spawnCount ?? 0;
            _randomPoints = GetRandomPoints();
          }
        });
      }

      var container = new VisualElement();
      container.style.flexDirection = FlexDirection.Row;
      container.style.justifyContent = Justify.SpaceBetween;
      container.Add(dropdown);
      container.Add(new Button(() => {
        var path = EditorUtility.SaveFilePanel("Save Preset As", "Assets/Data/", "SnapPlacerPreset", "asset");
        path = path.Substring(path.IndexOf("Assets"));
        AssetDatabaseUtils.CreateAsset<SnapPlacerPreset>(path);

        rootVisualElement.Clear();
        CreateGUI();
      }) { text = "Create New" });

      rootVisualElement.Add(container);

      if (preset) {
        rootVisualElement.Add(createPresetEditor());
        _spawnCount = preset?.spawnCount ?? 0;
        _randomPoints = GetRandomPoints();
      }
    }

    VisualElement createPresetEditor() {
      var editor = Editor.CreateEditor(preset);
      return new IMGUIContainer(editor.OnInspectorGUI) { name = "editor-container" };
    }

    Vector2[] GetRandomPoints() {
      var points = new Vector2[_spawnCount];
      for (var i = 0; i < _spawnCount; i++) {
        var newPoint = Random.insideUnitCircle;
        var minDistance = 2f;
        foreach (var other in points) {
          if (other == Vector2.zero) {
            continue;
          }
          var d = Vector3.SqrMagnitude((newPoint - other));
          if (d < minDistance) {
            minDistance = d;
          }
        }
        if (Mathf.Sqrt(minDistance) > preset.minimumSpaceBetween / preset.radius) {
          points[i] = newPoint;
        }
      }

      return points;
    }

    void drawSphere(Vector3 pos) {
      Handles.SphereHandleCap(-1, pos, Quaternion.identity, 0.1f, EventType.Repaint);
    }

    void DuringSceneGUI(SceneView sceneView) {
      if (!this.hasFocus || preset == null) {
        return;
      }

      if (_spawnCount != preset.spawnCount) {
        _spawnCount = preset.spawnCount;
        _randomPoints = GetRandomPoints();
      }

      Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;
      var camTF = sceneView.camera.transform;
      HandleMouseEvents(sceneView);

      var ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);

      if (Physics.Raycast(ray, out RaycastHit hit)) {
        int controlID = GUIUtility.GetControlID(FocusType.Passive);
        var hitTanget = Vector3.Cross(hit.normal, camTF.up).normalized;
        var hitBiTanget = Vector3.Cross(hit.normal, hitTanget);

        foreach (var point in _randomPoints) {
          if (point != Vector2.zero) {
            DrawPreviews(hit, hitTanget, hitBiTanget, point);
          }
        }

        if (Event.current.type == EventType.Layout) {
          HandleUtility.AddDefaultControl(controlID);
        }

        if (Event.current.type == EventType.MouseDown && Event.current.button == 0) {
          Event.current.Use();
          foreach (var point in _randomPoints) {
            if (point != Vector2.zero) {
              PlacePrefab(hit, hitTanget, hitBiTanget, point);
            }
          }

          _randomPoints = GetRandomPoints();
        }

        DrawHandles(hit, hitTanget, hitBiTanget);
      }
    }

    private void HandleMouseEvents(SceneView sceneView) {
      var holdingShift = (Event.current.modifiers & EventModifiers.Shift) != 0;
      var holdingCtrl = (Event.current.modifiers & EventModifiers.Control) != 0;
      var holdingAlt = (Event.current.modifiers & EventModifiers.Alt) != 0;

      if (Event.current.type == EventType.ScrollWheel) {
        if (holdingShift) {
          float scrollDir = Mathf.Sign(Event.current.delta.y);
          preset.radius *= 1 + scrollDir * 0.1f;
        }

        if (holdingAlt) {
          float scrollDir = Mathf.Sign(Event.current.delta.y);
          preset.alignToSurface = Mathf.Clamp(preset.alignToSurface + scrollDir * 0.1f, 0, 1);
        }


        if (holdingCtrl) {
          float scrollDir = Mathf.Sign(Event.current.delta.y);
          preset.spawnCount = (int)Mathf.Clamp(preset.spawnCount + scrollDir, 1, 200);
        }

        if (holdingCtrl || holdingShift || holdingAlt) {
          Event.current.Use();
        }
      }

      if (Event.current.type == EventType.MouseMove) {
        sceneView.Repaint();
      }
    }

    private void DrawHandles(RaycastHit hit, Vector3 hitTanget, Vector3 hitBiTanget) {
      Handles.color = Color.red;
      Handles.DrawAAPolyLine(4, hit.point, hit.point + hit.normal);
      Handles.color = Color.green;
      Handles.DrawAAPolyLine(4, hit.point, hit.point + hitBiTanget);
      Handles.color = Color.blue;
      Handles.DrawAAPolyLine(4, hit.point, hit.point + hitTanget);

      Handles.color = Color.white;
      Handles.DrawWireDisc(hit.point, hit.normal, preset.radius);
    }

    private void DrawPreviews(RaycastHit hit, Vector3 hitTanget, Vector3 hitBiTanget, Vector2 point) {
      if (preset.prefabs.Count <= 0) {
        return;
      }

      var prefab = preset.prefabs[Random.Range(0, preset.prefabs.Count)];

      if (!prefab) {
        return;
      }

      var worldPos = hit.point + (hitTanget * point.x + hitBiTanget * point.y) * preset.radius;
      if (Physics.Raycast(worldPos + (hit.normal * 5), -hit.normal, out RaycastHit randHit)) {
        if (prefab.TryGetComponent<MeshFilter>(out var meshFilter)) {
          preset.previewMaterial.SetPass(0);
          var fwd = Vector3.Lerp(Vector3.forward, hitBiTanget, preset.alignToSurface);
          var up = Vector3.Lerp(Vector3.up, hit.normal, preset.alignToSurface);
          var randomRot = preset.randomizeRotation ? Quaternion.AngleAxis(Random.Range(0, 360), up) : Quaternion.identity;
          var mtx = Matrix4x4.TRS(randHit.point, randomRot * Quaternion.LookRotation(fwd, up), Vector3.one);
          Graphics.DrawMeshNow(meshFilter.sharedMesh, mtx * prefab.transform.localToWorldMatrix);
        }
        else {
          drawSphere(randHit.point);
        }
        Handles.DrawAAPolyLine(4, randHit.point, randHit.point + randHit.normal);
      }
    }

    private void PlacePrefab(RaycastHit hit, Vector3 hitTanget, Vector3 hitBiTanget, Vector2 point) {
      var prefabs = preset.prefabs;
      var random = Random.Range(0, prefabs.Count);
      var prefab = prefabs.Count > 0 ? prefabs[Random.Range(0, prefabs.Count)] : null;

      var worldPos = hit.point + (hitTanget * point.x + hitBiTanget * point.y) * preset.radius;

      if (Physics.Raycast(worldPos + (hit.normal * 5), -hit.normal, out RaycastHit randHit)) {
        var fwd = Vector3.Lerp(Vector3.forward, hitBiTanget, preset.alignToSurface);
        var up = Vector3.Lerp(Vector3.up, hit.normal, preset.alignToSurface);
        var randomRot = preset.randomizeRotation ? Quaternion.AngleAxis(Random.Range(0, 360), up) : Quaternion.identity;
        var obj = GameObject.Instantiate(prefab, randHit.point, randomRot * Quaternion.LookRotation(fwd, up) * prefab.transform.rotation);

        if (Selection.activeTransform) {
          obj.transform.SetParent(Selection.activeTransform);
        }

        Undo.RegisterCreatedObjectUndo(obj, "Snap Placer Placement");
      }
    }
  }
}
