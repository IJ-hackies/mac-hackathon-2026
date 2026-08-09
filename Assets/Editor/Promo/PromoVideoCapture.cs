using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace PromoCapture.Editor
{
    /// <summary>
    /// Deterministic, package-free promo capture tooling. Custom shots are sampled directly in
    /// edit mode; the real opening is captured in Play mode so it remains identical to gameplay.
    /// PNG sequences are written under the gitignored Recordings/Promo directory for FFmpeg.
    /// </summary>
    public static class PromoVideoCapture
    {
        internal const int Width = 1920;
        internal const int Height = 1080;
        internal const int FramesPerSecond = 30;
        internal const string ScenePath = "Assets/Scenes/SampleScene.unity";
        internal const string OutputRoot = "Recordings/Promo";

        private static bool s_BatchMode;
        private static bool s_WaitingForOpeningExit;
        private static int s_PreviousCaptureFramerate;
        private static readonly string RequestPath = Path.GetFullPath("Temp/PromoCapture.request");
        private static readonly string OpeningPendingPath = Path.GetFullPath("Temp/PromoOpening.pending");

        [InitializeOnLoadMethod]
        private static void InstallRequestWatcher()
        {
            EditorApplication.update -= WatchForCaptureRequest;
            EditorApplication.update += WatchForCaptureRequest;
            if (File.Exists(OpeningPendingPath))
            {
                EditorApplication.playModeStateChanged -= HandleOpeningPlayModeState;
                EditorApplication.playModeStateChanged += HandleOpeningPlayModeState;
            }
        }

        [MenuItem("Tools/Promo Video/Capture Space Float Frames")]
        public static void CaptureSpaceFloatMenu() => CaptureCustomShot(PromoShot.SpaceFloat);

        [MenuItem("Tools/Promo Video/Capture Empty Planet Emotes Frames")]
        public static void CapturePlanetEmotesMenu() => CaptureCustomShot(PromoShot.PlanetEmotes);

        [MenuItem("Tools/Promo Video/Capture Entity Rave Frames")]
        public static void CaptureRaveMenu() => CaptureCustomShot(PromoShot.Rave);

        [MenuItem("Tools/Promo Video/Capture Starting Cutscene Frames")]
        public static void CaptureOpeningMenu()
        {
            s_BatchMode = false;
            BeginOpeningCapture();
        }

        /// <summary>
        /// Batch entry point. Pass one of opening, space_float, planet_emotes, or rave via
        /// -promoShot. Example: Unity.exe -batchmode -projectPath ... -executeMethod
        /// PromoCapture.Editor.PromoVideoCapture.CaptureFromCommandLine -promoShot rave
        /// </summary>
        public static void CaptureFromCommandLine()
        {
            s_BatchMode = Application.isBatchMode;
            try
            {
                string value = ReadCommandLineValue("-promoShot");
                PromoShot shot = ParseShot(value);
                if (shot == PromoShot.Opening)
                {
                    BeginOpeningCapture();
                    return;
                }

                CaptureCustomShot(shot);
                if (s_BatchMode) EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (s_BatchMode) EditorApplication.Exit(1);
                else throw;
            }
        }

        private static void WatchForCaptureRequest()
        {
            if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode ||
                !File.Exists(RequestPath))
            {
                return;
            }

            string requestedShot = File.ReadAllText(RequestPath).Trim();
            File.Delete(RequestPath);
            try
            {
                PromoShot shot = ParseShot(requestedShot);
                if (shot == PromoShot.Opening) BeginOpeningCapture();
                else CaptureCustomShot(shot);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private static void BeginOpeningCapture()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("Exit Play mode before starting a promo capture.");
            }

            PrepareOutputDirectory(PromoShot.Opening);
            File.WriteAllText(OpeningPendingPath, "opening");
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            s_PreviousCaptureFramerate = Time.captureFramerate;
            Time.captureFramerate = FramesPerSecond;
            EditorApplication.playModeStateChanged -= HandleOpeningPlayModeState;
            EditorApplication.playModeStateChanged += HandleOpeningPlayModeState;
            EditorApplication.isPlaying = true;
        }

        private static void HandleOpeningPlayModeState(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                OpeningCutsceneController opening = UnityEngine.Object.FindFirstObjectByType<OpeningCutsceneController>();
                Camera camera = FindGameplayCamera();
                if (opening == null || camera == null)
                {
                    FinishOpeningCapture(false, "Opening cutscene or gameplay camera was not found.");
                    return;
                }

                var host = new GameObject("Promo Opening Frame Capture");
                host.AddComponent<OpeningFrameCaptureDriver>().Configure(
                    opening,
                    camera,
                    GetOutputDirectory(PromoShot.Opening));
            }
            else if (state == PlayModeStateChange.EnteredEditMode &&
                     (s_WaitingForOpeningExit || File.Exists(OpeningPendingPath)))
            {
                s_WaitingForOpeningExit = false;
                EditorApplication.playModeStateChanged -= HandleOpeningPlayModeState;
                Time.captureFramerate = s_PreviousCaptureFramerate;
                if (File.Exists(OpeningPendingPath)) File.Delete(OpeningPendingPath);
                Debug.Log($"Promo opening frames captured to {GetOutputDirectory(PromoShot.Opening)}");
                if (s_BatchMode) EditorApplication.Exit(0);
            }
        }

        internal static void FinishOpeningCapture(bool success, string error)
        {
            if (!success)
            {
                Debug.LogError(error);
            }

            if (s_BatchMode && !success)
            {
                EditorApplication.Exit(1);
                return;
            }

            s_WaitingForOpeningExit = true;
            EditorApplication.isPlaying = false;
        }

        private static void CaptureCustomShot(PromoShot shot)
        {
            if (shot == PromoShot.Opening)
            {
                BeginOpeningCapture();
                return;
            }

            string outputDirectory = PrepareOutputDirectory(shot);
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            PromoStage stage = null;

            try
            {
                stage = PromoStage.Create(scene, shot);
                int frameCount = Mathf.CeilToInt(stage.Duration * FramesPerSecond);
                using (var writer = new PromoFrameWriter(stage.Camera, outputDirectory))
                {
                    for (int frame = 0; frame < frameCount; frame++)
                    {
                        float time = frame / (float)FramesPerSecond;
                        stage.Sample(time);
                        writer.Capture(frame);
                        if (frame % FramesPerSecond == 0)
                        {
                            Debug.Log($"Promo {ShotSlug(shot)}: {frame}/{frameCount} frames");
                        }
                    }
                }

                Debug.Log($"Captured {frameCount} promo frames to {outputDirectory}");
            }
            finally
            {
                stage?.Dispose();
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }
        }

        private static Camera FindGameplayCamera()
        {
            ThirdPersonCameraController controller =
                UnityEngine.Object.FindFirstObjectByType<ThirdPersonCameraController>(FindObjectsInactive.Include);
            if (controller != null)
            {
                Camera nested = controller.GetComponentInChildren<Camera>(true);
                if (nested != null) return nested;
            }

            return Camera.main;
        }

        private static string PrepareOutputDirectory(PromoShot shot)
        {
            string directory = GetOutputDirectory(shot);
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
            Directory.CreateDirectory(directory);
            return directory;
        }

        private static string GetOutputDirectory(PromoShot shot) =>
            Path.GetFullPath(Path.Combine(OutputRoot, "frames", ShotSlug(shot)));

        private static string ReadCommandLineValue(string name)
        {
            string[] arguments = Environment.GetCommandLineArgs();
            for (int index = 0; index < arguments.Length - 1; index++)
            {
                if (string.Equals(arguments[index], name, StringComparison.OrdinalIgnoreCase))
                {
                    return arguments[index + 1];
                }
            }

            throw new ArgumentException($"Missing required command-line argument {name}.");
        }

        private static PromoShot ParseShot(string value)
        {
            switch (value?.Trim().ToLowerInvariant())
            {
                case "opening": return PromoShot.Opening;
                case "space_float": return PromoShot.SpaceFloat;
                case "planet_emotes": return PromoShot.PlanetEmotes;
                case "rave": return PromoShot.Rave;
                default: throw new ArgumentException($"Unknown promo shot '{value}'.");
            }
        }

        internal static string ShotSlug(PromoShot shot)
        {
            switch (shot)
            {
                case PromoShot.Opening: return "starting_cutscene";
                case PromoShot.SpaceFloat: return "space_float";
                case PromoShot.PlanetEmotes: return "empty_planet_emotes";
                case PromoShot.Rave: return "entity_rave";
                default: throw new ArgumentOutOfRangeException(nameof(shot), shot, null);
            }
        }
    }

    internal enum PromoShot
    {
        Opening,
        SpaceFloat,
        PlanetEmotes,
        Rave
    }

    [DefaultExecutionOrder(32000)]
    internal sealed class OpeningFrameCaptureDriver : MonoBehaviour
    {
        private OpeningCutsceneController _opening;
        private Camera _camera;
        private string _outputDirectory;
        private PromoFrameWriter _writer;
        private bool _started;
        private bool _finishing;
        private int _frame;

        public void Configure(OpeningCutsceneController opening, Camera camera, string outputDirectory)
        {
            _opening = opening;
            _camera = camera;
            _outputDirectory = outputDirectory;
            Time.captureFramerate = PromoVideoCapture.FramesPerSecond;
            _opening.SetCaptureDeltaTimeOverride(1f / PromoVideoCapture.FramesPerSecond);
            _writer = new PromoFrameWriter(_camera, _outputDirectory);
        }

        private void LateUpdate()
        {
            if (_finishing || _opening == null || _writer == null) return;

            if (!_started)
            {
                if (!_opening.IsPlaying)
                {
                    if (_opening.IsCompleted)
                    {
                        Fail("Opening cutscene completed before frame capture began.");
                    }
                    return;
                }

                _started = true;
            }

            try
            {
                _writer.Capture(_frame++);
                if (_opening.IsCompleted)
                {
                    _finishing = true;
                    _writer.Dispose();
                    _writer = null;
                    Debug.Log($"Captured {_frame} opening-cutscene frames.");
                    PromoVideoCapture.FinishOpeningCapture(true, null);
                }
                else if (_frame > PromoVideoCapture.FramesPerSecond * 22)
                {
                    Fail("Opening capture exceeded the 22-second safety limit.");
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Fail("Opening capture failed while rendering a frame.");
            }
        }

        private void OnDestroy()
        {
            _writer?.Dispose();
            _writer = null;
        }

        private void Fail(string message)
        {
            _finishing = true;
            _writer?.Dispose();
            _writer = null;
            PromoVideoCapture.FinishOpeningCapture(false, message);
        }
    }

    internal sealed class PromoFrameWriter : IDisposable
    {
        private readonly Camera _camera;
        private readonly string _directory;
        private readonly RenderTexture _target;
        private readonly Texture2D _readback;
        private readonly RenderTexture _previousTarget;

        public PromoFrameWriter(Camera camera, string directory)
        {
            _camera = camera != null ? camera : throw new ArgumentNullException(nameof(camera));
            _directory = directory;
            Directory.CreateDirectory(directory);
            _previousTarget = camera.targetTexture;
            _target = new RenderTexture(
                PromoVideoCapture.Width,
                PromoVideoCapture.Height,
                24,
                RenderTextureFormat.ARGB32)
            {
                name = "Promo 1080p Capture Target",
                antiAliasing = 1,
                useMipMap = false
            };
            _target.Create();
            _readback = new Texture2D(
                PromoVideoCapture.Width,
                PromoVideoCapture.Height,
                TextureFormat.RGB24,
                false);
            camera.targetTexture = _target;
        }

        public void Capture(int frame)
        {
            RenderTexture previousActive = RenderTexture.active;
            try
            {
                _camera.Render();
                RenderTexture.active = _target;
                _readback.ReadPixels(
                    new Rect(0f, 0f, PromoVideoCapture.Width, PromoVideoCapture.Height),
                    0,
                    0,
                    false);
                _readback.Apply(false, false);
                string path = Path.Combine(_directory, $"frame_{frame:D6}.png");
                File.WriteAllBytes(path, _readback.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previousActive;
            }
        }

        public void Dispose()
        {
            if (_camera != null) _camera.targetTexture = _previousTarget;
            if (_readback != null) UnityEngine.Object.DestroyImmediate(_readback);
            if (_target != null)
            {
                _target.Release();
                UnityEngine.Object.DestroyImmediate(_target);
            }
        }
    }

    internal sealed class PromoStage : IDisposable
    {
        private static readonly string CharacterRoot = "Assets/Art/Models/Characters/";
        private readonly PromoShot _shot;
        private readonly GameObject _root;
        private readonly List<PromoActor> _actors = new List<PromoActor>();
        private readonly List<Light> _partyLights = new List<Light>();
        private readonly Transform _planet;
        private readonly MeshCollider _planetCollider;
        private readonly Vector3 _planetCenter;
        private readonly Vector3 _surfaceUp;
        private readonly Vector3 _surfaceRight;
        private readonly Vector3 _surfaceForward;
        private readonly Vector3 _surfaceCenter;

        public Camera Camera { get; }
        public float Duration { get; }

        private PromoStage(Scene scene, PromoShot shot)
        {
            _shot = shot;
            Duration = shot == PromoShot.SpaceFloat ? 8f : shot == PromoShot.PlanetEmotes ? 10f : 12f;
            bool needsPlanet = shot != PromoShot.SpaceFloat;
            DeactivateGameplayRoots(scene, needsPlanet);

            _root = new GameObject($"Promo Stage - {PromoVideoCapture.ShotSlug(shot)}");
            SceneManager.MoveGameObjectToScene(_root, scene);
            Camera = CreateCamera(_root.transform);
            CreateKeyLight(_root.transform);

            if (needsPlanet)
            {
                _planet = FindRoot(scene, "Planet Ground")?.transform;
                if (_planet == null) throw new InvalidOperationException("Planet Ground root was not found.");
                foreach (Behaviour behaviour in _planet.GetComponentsInChildren<Behaviour>(true))
                {
                    behaviour.enabled = false;
                }

                _planetCollider = _planet.GetComponentsInChildren<MeshCollider>(true)
                    .FirstOrDefault(collider => collider.sharedMesh != null);
                if (_planetCollider == null) throw new InvalidOperationException("Planet mesh collider was not found.");
                _planetCenter = _planet.position;
                _surfaceUp = Vector3.up;
                _surfaceRight = Vector3.right;
                _surfaceForward = Vector3.forward;
                _surfaceCenter = FindSurfacePoint(_surfaceUp, out _);
            }

            switch (shot)
            {
                case PromoShot.SpaceFloat:
                    BuildSpaceFloat();
                    break;
                case PromoShot.PlanetEmotes:
                    BuildPlanetEmotes();
                    break;
                case PromoShot.Rave:
                    BuildRave();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(shot), shot, null);
            }
        }

        public static PromoStage Create(Scene scene, PromoShot shot) => new PromoStage(scene, shot);

        public void Sample(float time)
        {
            switch (_shot)
            {
                case PromoShot.SpaceFloat:
                    SampleSpaceFloat(time);
                    break;
                case PromoShot.PlanetEmotes:
                    SamplePlanetEmotes(time);
                    break;
                case PromoShot.Rave:
                    SampleRave(time);
                    break;
            }
        }

        public void Dispose()
        {
            foreach (PromoActor actor in _actors) actor.Dispose();
            if (_root != null) UnityEngine.Object.DestroyImmediate(_root);
        }

        private void BuildSpaceFloat()
        {
            PromoActor astronaut = AddActor(
                "Floating Finn",
                "Astronaut_FinnTheFrog.fbx",
                "Assets/Art/Materials/M_Astronaut.mat",
                3.6f,
                "Jump_Idle");
            astronaut.Anchor.position = new Vector3(-12f, 0f, 4f);
            Camera.transform.SetPositionAndRotation(
                new Vector3(0f, 0.4f, -15f),
                Quaternion.LookRotation(Vector3.forward, Vector3.up));
            Camera.fieldOfView = 43f;
        }

        private void BuildPlanetEmotes()
        {
            PromoActor astronaut = AddActor(
                "Finn Emotes",
                "Astronaut_FinnTheFrog.fbx",
                "Assets/Art/Materials/M_Astronaut.mat",
                3.4f,
                "Wave", "Yes", "No", "Duck", "Punch");
            PlaceActor(astronaut, 0f, 0f, 0f);
        }

        private void BuildRave()
        {
            AddRaveActor("Finn", "Astronaut_FinnTheFrog.fbx", "M_Astronaut.mat", 3.1f, -6f, 1f,
                "Wave", "Yes", "No", "Punch", "Duck");
            AddRaveActor("Finn Mech", "Mech_FinnTheFrog.fbx", "M_MechFinnTheFrog.mat", 4.3f, -2f, 2.5f,
                "Dance", "Hello", "Yes", "No", "Kick");
            AddRaveActor("Barbara", "Astronaut_BarbaraTheBee.fbx", "M_Astronaut_BarbaraTheBee.mat", 3.1f, 2f, 2.5f,
                "Wave", "Yes", "No", "Punch", "Duck");
            AddRaveActor("Barbara Mech", "Mech_BarbaraTheBee.fbx", "M_Mech_BarbaraTheBee.mat", 4.3f, 6f, 1f,
                "Dance", "Hello", "Yes", "No", "Kick");
            AddRaveActor("Flying Enemy", "Enemy_Flying.fbx", "M_EnemyFlying.mat", 2.8f, -5f, -3.5f,
                "Yes", "No", "Punch", "Fast_Flying", "Flying_Idle");
            AddRaveActor("Small Enemy", "Enemy_Small.fbx", "M_EnemySmall.mat", 2.8f, 0f, -4f,
                "Yes", "No", "Punch", "Headbutt", "Fast_Flying");
            AddRaveActor("Large Enemy", "Enemy_Large.fbx", "M_EnemyLarge.mat", 4.2f, 5f, -3.5f,
                "Wave", "Yes", "No", "Jump", "Punch");

            for (int index = 0; index < 4; index++)
            {
                var lightObject = new GameObject($"Party Light {index + 1}");
                lightObject.transform.SetParent(_root.transform, false);
                Light light = lightObject.AddComponent<Light>();
                light.type = LightType.Point;
                light.range = 28f;
                light.intensity = 13f;
                light.shadows = LightShadows.None;
                _partyLights.Add(light);
            }
        }

        private void SampleSpaceFloat(float time)
        {
            PromoActor actor = _actors[0];
            actor.Sample("Jump_Idle", time * 0.72f);
            float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(time / Duration));
            actor.Anchor.position = new Vector3(
                Mathf.Lerp(-13.5f, 13.5f, progress),
                0.55f * Mathf.Sin(time * 1.35f),
                4f + 0.35f * Mathf.Sin(time * 0.7f));
            actor.Anchor.rotation = Quaternion.Euler(
                18f * Mathf.Sin(time * 0.9f),
                time * 34f,
                time * -112f);
            Camera.transform.position = new Vector3(0f, 0.4f, -15f);
            Camera.transform.rotation = LookAt(Camera.transform.position, new Vector3(0f, 0f, 4f), Vector3.up);
        }

        private void SamplePlanetEmotes(float time)
        {
            PromoActor actor = _actors[0];
            string[] sequence = { "Wave", "Yes", "No", "Duck", "Punch" };
            int index = Mathf.Min(sequence.Length - 1, Mathf.FloorToInt(time / 2f));
            actor.Sample(sequence[index], time - index * 2f);
            actor.ApplyGroundedMotion(
                0.08f * Mathf.Sin(time * 2.2f),
                5f * Mathf.Sin(time * 0.8f));

            float orbit = Mathf.Lerp(-22f, 24f, time / Duration) * Mathf.Deg2Rad;
            Vector3 horizontal =
                _surfaceRight * Mathf.Sin(orbit) * 11.5f -
                _surfaceForward * Mathf.Cos(orbit) * 11.5f;
            Vector3 position = _surfaceCenter + _surfaceUp * 5.2f + horizontal;
            Camera.transform.SetPositionAndRotation(
                position,
                LookAt(position, _surfaceCenter + _surfaceUp * 1.55f, _surfaceUp));
            Camera.fieldOfView = 40f;
        }

        private void SampleRave(float time)
        {
            for (int index = 0; index < _actors.Count; index++)
            {
                PromoActor actor = _actors[index];
                float phase = index * 0.83f;
                int clipIndex = Mathf.FloorToInt((time + index * 0.47f) / 2.15f) % actor.ClipNames.Count;
                actor.Sample(actor.ClipNames[clipIndex], time + phase);
                float bounce = 0.18f + 0.22f * Mathf.Max(0f, Mathf.Sin(time * 4.2f + phase));
                float yaw = 13f * Mathf.Sin(time * (0.9f + index * 0.04f) + phase);
                actor.ApplyGroundedMotion(bounce, yaw);
            }

            for (int index = 0; index < _partyLights.Count; index++)
            {
                float angle = time * (0.7f + index * 0.08f) + index * Mathf.PI * 0.5f;
                Light light = _partyLights[index];
                light.transform.position = _surfaceCenter +
                                           _surfaceRight * Mathf.Cos(angle) * 10f +
                                           _surfaceForward * Mathf.Sin(angle) * 10f +
                                           _surfaceUp * (5f + 2f * Mathf.Sin(time * 1.7f + index));
                light.color = Color.HSVToRGB(Mathf.Repeat(time * 0.11f + index * 0.23f, 1f), 0.78f, 1f);
                light.intensity = 11f + 5f * (0.5f + 0.5f * Mathf.Sin(time * 5f + index));
            }

            float orbit = (-38f + time * 7.4f) * Mathf.Deg2Rad;
            float push = 20.5f + 1.2f * Mathf.Sin(time * 0.65f);
            Vector3 position = _surfaceCenter +
                               _surfaceUp * (9.5f + Mathf.Sin(time * 0.8f)) +
                               _surfaceRight * Mathf.Sin(orbit) * push -
                               _surfaceForward * Mathf.Cos(orbit) * push;
            Camera.transform.SetPositionAndRotation(
                position,
                LookAt(position, _surfaceCenter + _surfaceUp * 1.9f, _surfaceUp));
            Camera.fieldOfView = 48f + 2f * Mathf.Sin(time * 1.2f);
        }

        private PromoActor AddRaveActor(
            string name,
            string model,
            string material,
            float height,
            float x,
            float z,
            params string[] clips)
        {
            PromoActor actor = AddActor(
                name,
                model,
                $"Assets/Art/Materials/{material}",
                height,
                clips);
            PlaceActor(actor, x, z, 0f);
            return actor;
        }

        private PromoActor AddActor(
            string name,
            string modelFile,
            string materialPath,
            float targetHeight,
            params string[] clips)
        {
            PromoActor actor = PromoActor.Create(
                _root.transform,
                name,
                CharacterRoot + modelFile,
                materialPath,
                targetHeight,
                clips);
            _actors.Add(actor);
            return actor;
        }

        private void PlaceActor(PromoActor actor, float x, float z, float facingYaw)
        {
            float radius = Mathf.Max(1f, Vector3.Distance(_surfaceCenter, _planetCenter));
            Vector3 direction = (_surfaceUp * radius + _surfaceRight * x + _surfaceForward * z).normalized;
            Vector3 point = FindSurfacePoint(direction, out Vector3 normal);
            Vector3 forward = Vector3.ProjectOnPlane(-_surfaceForward, normal).normalized;
            if (forward.sqrMagnitude < 0.001f) forward = Vector3.ProjectOnPlane(Vector3.forward, normal).normalized;
            Quaternion rotation = Quaternion.LookRotation(forward, normal) * Quaternion.Euler(0f, facingYaw, 0f);
            actor.SetGroundPose(point + normal * 0.04f, rotation, normal);
        }

        private Vector3 FindSurfacePoint(Vector3 radialDirection, out Vector3 normal)
        {
            Vector3 direction = radialDirection.normalized;
            Ray ray = new Ray(_planetCenter + direction * 260f, -direction);
            if (_planetCollider.Raycast(ray, out RaycastHit hit, 520f))
            {
                normal = hit.normal.normalized;
                return hit.point;
            }

            normal = direction;
            return _planetCenter + direction * 150f;
        }

        private static void DeactivateGameplayRoots(Scene scene, bool keepPlanet)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                bool keep = root.name == "Sun Light" ||
                            root.name == "Global Volume" ||
                            (keepPlanet && root.name == "Planet Ground");
                root.SetActive(keep);
            }
        }

        private static GameObject FindRoot(Scene scene, string name) =>
            scene.GetRootGameObjects().FirstOrDefault(root => root.name == name);

        private static Camera CreateCamera(Transform parent)
        {
            foreach (Camera existing in UnityEngine.Object.FindObjectsByType<Camera>(
                         FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
            {
                existing.enabled = false;
            }

            var cameraObject = new GameObject("Promo Camera");
            cameraObject.transform.SetParent(parent, false);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 1200f;
            camera.allowHDR = true;
            camera.allowMSAA = false;
            UniversalAdditionalCameraData data = camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.renderShadows = true;
            data.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
            return camera;
        }

        private static void CreateKeyLight(Transform parent)
        {
            var lightObject = new GameObject("Promo Fill Light");
            lightObject.transform.SetParent(parent, false);
            lightObject.transform.rotation = Quaternion.Euler(32f, -28f, 0f);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.62f, 0.72f, 1f);
            light.intensity = 0.7f;
            light.shadows = LightShadows.None;
        }

        private static Quaternion LookAt(Vector3 position, Vector3 target, Vector3 preferredUp)
        {
            Vector3 forward = (target - position).normalized;
            Vector3 up = Vector3.ProjectOnPlane(preferredUp, forward).normalized;
            if (up.sqrMagnitude < 0.001f) up = Vector3.up;
            return Quaternion.LookRotation(forward, up);
        }
    }

    internal sealed class PromoActor : IDisposable
    {
        private readonly GameObject _model;
        private readonly Dictionary<string, AnimationClip> _clips;
        private Vector3 _groundPosition;
        private Quaternion _groundRotation;
        private Vector3 _groundUp = Vector3.up;

        public Transform Anchor { get; }
        public IReadOnlyList<string> ClipNames { get; }

        private PromoActor(
            Transform parent,
            string name,
            GameObject model,
            Dictionary<string, AnimationClip> clips,
            IReadOnlyList<string> clipNames)
        {
            var anchorObject = new GameObject(name);
            Anchor = anchorObject.transform;
            Anchor.SetParent(parent, false);
            _model = model;
            _model.transform.SetParent(Anchor, false);
            _clips = clips;
            ClipNames = clipNames;
        }

        public static PromoActor Create(
            Transform parent,
            string name,
            string modelPath,
            string materialPath,
            float targetHeight,
            params string[] requestedClips)
        {
            GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (modelAsset == null) throw new FileNotFoundException("Character model not found.", modelPath);
            GameObject model = UnityEngine.Object.Instantiate(modelAsset);
            model.name = Path.GetFileNameWithoutExtension(modelPath);
            model.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            model.transform.localScale = Vector3.one;

            foreach (Animator animator in model.GetComponentsInChildren<Animator>(true))
            {
                animator.enabled = false;
                animator.applyRootMotion = false;
            }

            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null) throw new FileNotFoundException("Character material not found.", materialPath);
            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                renderer.sharedMaterials = Enumerable.Repeat(material, renderer.sharedMaterials.Length).ToArray();
            }

            AnimationClip[] allClips = AssetDatabase.LoadAllAssetsAtPath(modelPath)
                .OfType<AnimationClip>()
                .Where(clip => !clip.name.StartsWith("__preview__", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            var resolved = new Dictionary<string, AnimationClip>(StringComparer.OrdinalIgnoreCase);
            foreach (string requested in requestedClips)
            {
                AnimationClip clip = allClips.FirstOrDefault(candidate =>
                    string.Equals(candidate.name, requested, StringComparison.OrdinalIgnoreCase) ||
                    candidate.name.EndsWith("|" + requested, StringComparison.OrdinalIgnoreCase));
                if (clip == null)
                {
                    UnityEngine.Object.DestroyImmediate(model);
                    throw new InvalidOperationException($"Clip '{requested}' was not found in {modelPath}.");
                }
                resolved[requested] = clip;
            }

            var actor = new PromoActor(parent, name, model, resolved, requestedClips);
            actor.Sample(requestedClips[0], 0f);
            actor.NormalizeModel(targetHeight);
            return actor;
        }

        public void SetGroundPose(Vector3 position, Quaternion rotation, Vector3 up)
        {
            _groundPosition = position;
            _groundRotation = rotation;
            _groundUp = up.normalized;
            Anchor.SetPositionAndRotation(position, rotation);
        }

        public void ApplyGroundedMotion(float lift, float yaw)
        {
            Anchor.position = _groundPosition + _groundUp * lift;
            Anchor.rotation = Quaternion.AngleAxis(yaw, _groundUp) * _groundRotation;
        }

        public void Sample(string clipName, float time)
        {
            if (!_clips.TryGetValue(clipName, out AnimationClip clip))
            {
                throw new InvalidOperationException($"Actor {Anchor.name} does not have clip {clipName}.");
            }
            float sampleTime = clip.length > 0.0001f ? Mathf.Repeat(Mathf.Max(0f, time), clip.length) : 0f;
            clip.SampleAnimation(_model, sampleTime);
        }

        public void Dispose()
        {
            if (Anchor != null) UnityEngine.Object.DestroyImmediate(Anchor.gameObject);
        }

        private void NormalizeModel(float targetHeight)
        {
            Renderer[] renderers = _model.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) throw new InvalidOperationException($"Actor {Anchor.name} has no renderers.");
            Bounds bounds = renderers[0].bounds;
            for (int index = 1; index < renderers.Length; index++) bounds.Encapsulate(renderers[index].bounds);
            float height = Mathf.Max(0.001f, bounds.size.y);
            Vector3 feetCenter = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            _model.transform.position -= feetCenter;
            Anchor.localScale = Vector3.one * (targetHeight / height);
        }
    }
}
