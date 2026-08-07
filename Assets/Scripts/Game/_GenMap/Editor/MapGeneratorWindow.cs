using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Threading;
using UnityEditor;
using UnityEngine;

namespace FlowFree.GenMap
{
    public class MapGeneratorWindow : EditorWindow
    {
        private const string OutputRoot = "Assets/Resources/Maps/Generated";

        private readonly GenConfig _config = new GenConfig();

        private readonly ConcurrentQueue<int[,]> _queue = new ConcurrentQueue<int[,]>();
        private Thread _worker;
        private volatile bool _stop;
        private volatile bool _running;
        private int _produced;
        private volatile string _status = "Idle.";
        private volatile string _workerError;

        private string _batchId;
        private string _batchAbsFolder;
        private string _batchAssetFolder;
        private int _written;
        private bool _finalizePending;
        private string _uiError;

        /// <summary>
        /// Opens (or focuses) the Map Generator editor window.
        /// </summary>
        [MenuItem("Tools/FlowFree/Map Generator")]
        public static void ShowWindow()
        {
            GetWindow<MapGeneratorWindow>("Map Generator");
        }

        /// <summary>
        /// Subscribes the main-thread poller that drains generated maps to disk.
        /// </summary>
        private void OnEnable()
        {
            EditorApplication.update += OnEditorUpdate;
        }

        /// <summary>
        /// Unsubscribes the poller and stops any running worker when the window closes.
        /// </summary>
        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            RequestStopAndJoin();
        }

        /// <summary>
        /// Draws the configuration fields, Generate/Stop buttons, and progress readout.
        /// </summary>
        private void OnGUI()
        {
            EditorGUILayout.LabelField("Map Generation (Z3)", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(_running))
            {
                _config.Width = EditorGUILayout.IntField("Width", _config.Width);
                _config.Height = EditorGUILayout.IntField("Height", _config.Height);
                _config.ColorCount = EditorGUILayout.IntField("Colors", _config.ColorCount);
                _config.MapCount = EditorGUILayout.IntField("Maps to generate", _config.MapCount);
                _config.MinLength = EditorGUILayout.IntField("Min path length", _config.MinLength);
                _config.MaxLength = EditorGUILayout.IntField("Max path length", _config.MaxLength);
                _config.Seed = EditorGUILayout.IntField("Random seed", _config.Seed);
                _config.PerSolveTimeoutMs = EditorGUILayout.IntField("Per-solve timeout (ms)", _config.PerSolveTimeoutMs);
                _config.ResetSolverPerMap = EditorGUILayout.Toggle(
                    new GUIContent("Reset solver per map", "Faster; accepted maps don't constrain later ones (may repeat)."),
                    _config.ResetSolverPerMap);
            }

            EditorGUILayout.Space();

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(_running))
                {
                    if (GUILayout.Button("Generate", GUILayout.Height(28)))
                    {
                        StartGeneration();
                    }
                }
                using (new EditorGUI.DisabledScope(!_running))
                {
                    if (GUILayout.Button("Stop", GUILayout.Height(28)))
                    {
                        _stop = true;
                        _status = "Stopping...";
                    }
                }
            }

            EditorGUILayout.Space();

            if (!string.IsNullOrEmpty(_uiError))
            {
                EditorGUILayout.HelpBox(_uiError, MessageType.Error);
            }

            float progress = _config.MapCount > 0 ? Mathf.Clamp01((float)_written / _config.MapCount) : 0f;
            Rect bar = EditorGUILayout.GetControlRect(false, 20f);
            EditorGUI.ProgressBar(bar, progress, $"Written {_written}/{_config.MapCount}");

            EditorGUILayout.LabelField("Status", _status);
            if (!string.IsNullOrEmpty(_batchAssetFolder))
            {
                EditorGUILayout.LabelField("Batch", _batchAssetFolder);
            }
            if (!string.IsNullOrEmpty(_workerError))
            {
                EditorGUILayout.HelpBox(_workerError, MessageType.Error);
            }
        }

        /// <summary>
        /// Validates the config, creates the timestamped batch folder and manifest, and launches the worker thread.
        /// </summary>
        private void StartGeneration()
        {
            _uiError = null;
            _workerError = null;

            if (!_config.Validate(out string error))
            {
                _uiError = error;
                return;
            }

            _batchId = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            _batchAssetFolder = $"{OutputRoot}/{_batchId}";
            _batchAbsFolder = Path.Combine(Application.dataPath, "Resources/Maps/Generated", _batchId);
            Directory.CreateDirectory(_batchAbsFolder);
            WriteManifest();

            _queue.Clear();
            _produced = 0;
            _written = 0;
            _stop = false;
            _finalizePending = true;
            _running = true;
            _status = "Starting...";

            GenConfig snapshot = Clone(_config);
            _worker = new Thread(() => WorkerBody(snapshot)) { IsBackground = true, Name = "FlowFreeMapGen" };
            _worker.Start();
        }

        /// <summary>
        /// Background-thread entry point that runs generation and forwards results and errors to the UI.
        /// </summary>
        private void WorkerBody(GenConfig config)
        {
            try
            {
                MapGenerator generator = new MapGenerator(config);
                generator.Run(
                    shouldStop: () => _stop,
                    onMap: grid =>
                    {
                        _queue.Enqueue(grid);
                        Interlocked.Increment(ref _produced);
                    },
                    onStatus: s => _status = s);
            }
            catch (Exception e)
            {
                _workerError = e.Message;
            }
            finally
            {
                _running = false;
            }
        }

        /// <summary>
        /// Main-thread poll: writes any queued maps to CSV, repaints, and refreshes assets once generation ends.
        /// </summary>
        private void OnEditorUpdate()
        {
            bool wroteAny = false;
            while (_queue.TryDequeue(out int[,] grid))
            {
                if (CsvMapWriter.RoundTrips(grid, out string csv))
                {
                    string path = Path.Combine(_batchAbsFolder, $"{_written + 1}.csv");
                    CsvMapWriter.Write(path, csv);
                    _written++;
                    wroteAny = true;
                }
                else
                {
                    _workerError = "A generated map failed CSV round-trip and was skipped.";
                }
            }

            if (_running || wroteAny)
            {
                Repaint();
            }

            if (!_running && _queue.IsEmpty && _finalizePending)
            {
                _finalizePending = false;
                AssetDatabase.Refresh();
                Debug.Log($"[MapGenerator] Batch '{_batchId}' complete: {_written} map(s) in {_batchAssetFolder}.");
                Repaint();
            }
        }

        /// <summary>
        /// Writes a manifest.txt recording the config used for the current batch.
        /// </summary>
        private void WriteManifest()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"batchId={_batchId}");
            sb.AppendLine($"createdUtc={DateTime.UtcNow:o}");
            sb.AppendLine($"width={_config.Width}");
            sb.AppendLine($"height={_config.Height}");
            sb.AppendLine($"colors={_config.ColorCount}");
            sb.AppendLine($"maps={_config.MapCount}");
            sb.AppendLine($"minLength={_config.MinLength}");
            sb.AppendLine($"maxLength={_config.MaxLength}");
            sb.AppendLine($"seed={_config.Seed}");
            sb.AppendLine($"perSolveTimeoutMs={_config.PerSolveTimeoutMs}");
            sb.AppendLine($"resetSolverPerMap={_config.ResetSolverPerMap}");
            File.WriteAllText(Path.Combine(_batchAbsFolder, "manifest.txt"), sb.ToString());
        }

        /// <summary>
        /// Signals the worker to stop and waits briefly for it to finish.
        /// </summary>
        private void RequestStopAndJoin()
        {
            _stop = true;
            if (_worker != null && _worker.IsAlive)
            {
                _worker.Join(TimeSpan.FromMilliseconds(_config.PerSolveTimeoutMs + 1000));
            }
            _worker = null;
        }

        /// <summary>
        /// Copies the config so the worker thread reads a stable snapshot while the UI stays editable.
        /// </summary>
        private static GenConfig Clone(GenConfig src)
        {
            return new GenConfig
            {
                Width = src.Width,
                Height = src.Height,
                ColorCount = src.ColorCount,
                MapCount = src.MapCount,
                MinLength = src.MinLength,
                MaxLength = src.MaxLength,
                Seed = src.Seed,
                PerSolveTimeoutMs = src.PerSolveTimeoutMs,
                ResetSolverPerMap = src.ResetSolverPerMap
            };
        }
    }
}
