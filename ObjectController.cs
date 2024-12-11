// VERSION 0.0.1
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using Warudo.Core.Attributes;
using Warudo.Core.Graphs;
using Warudo.Plugins.Core.Assets;
using System.Threading.Tasks;

namespace sami6.Object
{
    [NodeType(
        Id = "com.sami6.ObjectController",
        Title = "Object Controller v0.0.1",
        Category = "SamyLabs"
    )]
    public class ObjectControllerNode : Warudo.Core.Graphs.Node
    {
        [DataInput]
        [Label("Character")]
        public GameObjectAsset characterAsset;

        // :q :q :q :q :q :q :q :q :q :q :q
        [DataInput]
        [Label("Asset 1")]
        public GameObjectAsset asset1;

        [DataInput]
        [Label("Asset 2")]
        public GameObjectAsset asset2;

        [DataInput]
        [Label("Asset 3")]
        public GameObjectAsset asset3;

        [DataInput]
        [Label("Asset 4")]
        public GameObjectAsset asset4;

        [DataInput]
        [Label("Asset 5")]
        public GameObjectAsset asset5;

        [DataInput]
        [Label("Asset 6")]
        public GameObjectAsset asset6;

        [DataInput]
        [Label("Asset 7")]
        public GameObjectAsset asset7;

        [DataInput]
        [Label("Asset 8")]
        public GameObjectAsset asset8;

        [DataInput]
        [Label("Asset 9")]
        public GameObjectAsset asset9;

        [DataInput]
        [Label("Asset 10")]
        public GameObjectAsset asset10;

        [DataInput]
        [Label("Commands")]
        public string[] commands;

        [DataOutput]
        [Label("Current Commands")]
        public string[] GetCurrentCommands()
        {
            return commands;
        }

        [DataInput]
        [Label("Shape Key Control")]
        public bool enableShapeKeyControl = true;

        [DataInput]
        [Label("Object Toggle")] 
        public bool enableObjectToggle = true; 

        [DataInput]
        [Label("Costume Control")]
        public bool enableCostumeControl = true;

        [DataInput]
        [Label("Command Delay (ms)")]
        [Range(0, 1000)]
        public float commandDelay = 0f;

        private string _currentCommand;
        private string _debugInfo;
        private List<string> _debugLogs = new List<string>();
        private Dictionary<string, bool> _previousObjectStates = new Dictionary<string, bool>();
        private bool _isProcessingCommands = false;
        private Dictionary<string, SkinnedMeshRenderer> rendererDict = new Dictionary<string, SkinnedMeshRenderer>();
        private Dictionary<string, List<string>> rendererShapeKeys = new Dictionary<string, List<string>>();
        private HashSet<string> _workLogs = new HashSet<string>();

        private void AddDebugLog(string message)
        {
            _debugLogs.Add($"[{DateTime.Now:HH:mm:ss}] {message}");
            if (_debugLogs.Count > 100)
            {
                _debugLogs.RemoveAt(0);
            }
        }

        private void AddWorkLog(string target, bool success)
        {
            if (string.IsNullOrEmpty(target)) return;
            _workLogs.Add($"{target}: {(success ? "작업완료" : "실패")}");
        }

        [DataOutput]
        public string GetCurrentCommand()
        {
            return _currentCommand;
        }

        [DataOutput]
        public string GetDebugInfo()
        {
            return _debugInfo;
        }

        [DataOutput]
        public string[] GetDebugLogs()
        {
            return _debugLogs.ToArray();
        }

        [DataOutput]
        public string[] GetWorkLogs()
        {
            return _workLogs.ToArray();
        }

        [FlowInput(1)]
        public Continuation Enter()
        {
            _workLogs.Clear();
            if (commands != null && commands.Length > 0)
            {
                _ = ProcessObjectCommandsAsync(commands);
            }
            return Exit;
        }

        [FlowInput(2)]
        public Continuation Reset()
        {
            ClearAllDictionaries();
            return Exit;
        }

        [FlowOutput]
        public Continuation Exit;

        private Dictionary<string, GameObject> objectDict = new Dictionary<string, GameObject>();
        private List<string> objectPaths = new List<string>();

        protected override void OnCreate()
        {
            Watch<GameObjectAsset>(nameof(characterAsset), (from, to) =>
            {
                if (to != null && to.Active)
                {
                    UpdateObjectDict(to.GameObject);
                    UpdateRendererDict(to.GameObject);
                    _debugInfo = $"캐릭터 '{to.Name}' 연결됨\n오브젝트 수: {objectDict.Count}\n렌더러 수: {rendererDict.Count}";
                }
            });

            for (int i = 1; i <= 10; i++)
            {
                string assetName = $"asset{i}";
                Watch<GameObjectAsset>(assetName, (from, to) =>
                {
                    if (to != null && to.Active)
                    {
                        UpdateObjectDict(to.GameObject);
                        UpdateRendererDict(to.GameObject);
                        _debugInfo = $"에셋 {i} '{to.Name}' 연결됨\n오브젝트 수: {objectDict.Count}\n렌더러 수: {rendererDict.Count}";
                    }
                });

                WatchAsset(assetName, () => {
                    var asset = GetType().GetField(assetName).GetValue(this) as GameObjectAsset;
                    if (asset != null && asset.Active)
                    {
                        UpdateObjectDict(asset.GameObject);
                        UpdateRendererDict(asset.GameObject);
                        _debugInfo = $"에셋 {i} '{asset.Name}' 연결됨\n오브젝트 수: {objectDict.Count}\n렌더러 수: {rendererDict.Count}";
                    }
                });
            }

            WatchAsset(nameof(characterAsset), () => {
                UpdateObjectDict(characterAsset.GameObject);
                UpdateRendererDict(characterAsset.GameObject);
                _debugInfo = $"캐릭터 '{characterAsset.Name}' 연결됨\n오브젝트 수: {objectDict.Count}\n렌더러 수: {rendererDict.Count}";
            });
        }

        private void UpdateRendererDict(GameObject root)
        {
            try
            {
                rendererDict.Clear();
                rendererShapeKeys.Clear();
                var renderers = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                AddDebugLog($"찾은 SkinnedMeshRenderer 수: {renderers.Length}");
                
                foreach (var renderer in renderers)
                {
                    if (renderer != null && renderer.sharedMesh != null)
                    {
                        string path = GetFullPath(renderer.transform);
                        rendererDict[path] = renderer;
                        
                        var shapeKeys = new List<string>();
                        for (int i = 0; i < renderer.sharedMesh.blendShapeCount; i++)
                        {
                            string shapeName = renderer.sharedMesh.GetBlendShapeName(i);
                            shapeKeys.Add(shapeName);
                        }
                        rendererShapeKeys[path] = shapeKeys;
                        
                        AddDebugLog($"렌더러 추가됨: {path}");
                        AddDebugLog($"블렌드쉐입 수: {shapeKeys.Count}");
                    }
                }
            }
            catch (Exception ex)
            {
                AddDebugLog($"렌더러 딕셔너리 업데이트 중 오류 발생: {ex}");
            }
        }

        private string GetFullPath(Transform transform)
        {
            List<string> pathParts = new List<string>();
            while (transform != null)
            {
                pathParts.Add(transform.name);
                transform = transform.parent;
            }
            pathParts.Reverse();
            return string.Join("/", pathParts);
        }

        private void ClearAllDictionaries()
        {
            objectDict.Clear();
            objectPaths.Clear();
            _previousObjectStates.Clear();
            rendererDict.Clear();
            rendererShapeKeys.Clear();
            _debugLogs.Clear();
            _workLogs.Clear();
            _debugInfo = "모든 상태가 초기화되었습니다.";
            AddDebugLog("모든 상태가 초기화되었습니다.");
        }

        private bool IsCostumeCommand(string command)
        {
            return command.StartsWith("ON_") || command.StartsWith("OFF_") || command.StartsWith("TO_");
        }

        public bool IsCommandEnabled(string command)
        {
            if (!command.EndsWith("_OBJ"))
            {
                return false;
            }

            if (command == "CLEAR_SAMYLABS_CLOTH")
            {
                return true;
            }

            if (IsCostumeCommand(command))
            {
                return enableCostumeControl;
            }

            if (command.StartsWith("BSK_"))
            {
                return enableShapeKeyControl;
            }
            else if (command.StartsWith("ON_") || command.StartsWith("OFF_") || command.StartsWith("TO_"))
            {
                return enableObjectToggle;
            }

            return false;
        }


        public async Task ProcessObjectCommandsAsync(string[] commands)
        {
            if (_isProcessingCommands)
            {
                AddDebugLog("이전 명령어들이 아직 처리 중입니다.");
                _debugInfo = "이전 명령어들이 아직 처리 중입니다.";
                return;
            }

            _isProcessingCommands = true;
            AddDebugLog($"명령어 처리 시작 - 총 {commands.Length}개의 명령어");

            try
            {
                if (commands.Any(cmd => cmd == "CLEAR_SAMYLABS_CLOTH"))
                {
                    _previousObjectStates.Clear();
                    _debugInfo = "의상 상태가 초기화되었습니다.";
                    AddDebugLog("의상 상태가 초기화되었습니다.");
                }

                var currentObjectStates = new Dictionary<string, bool>();
                foreach (var command in commands)
                {
                    if ((command.StartsWith("ON_") || command.StartsWith("OFF_")) && IsCommandEnabled(command))
                    {
                        string prefix = command.Substring(0, 3);
                        string objectName = command.Substring(prefix == "ON_" ? 3 : 4);
                        if (objectName.EndsWith("_OBJ"))
                        {
                            objectName = objectName.Substring(0, objectName.Length - 4);
                        }
                        currentObjectStates[objectName] = prefix == "ON_";
                        AddDebugLog($"ON/OFF 명령어 감지: {command} -> 오브젝트: {objectName}, 상태: {(prefix == "ON_" ? "활성화" : "비활성화")}");
                    }
                    else if (command.StartsWith("TO_") && IsCommandEnabled(command))
                    {
                        string objectName = command.Substring(3);
                        if (objectName.EndsWith("_OBJ"))
                        {
                            objectName = objectName.Substring(0, objectName.Length - 4);
                        }

                        bool stateChanged = false;

                        // 캐릭터에서 찾기
                        if (characterAsset != null && characterAsset.Active && enableObjectToggle)
                        {
                            string objectPath = FindObjectPath(objectName, characterAsset.GameObject);
                            if (objectPath != null && objectDict.TryGetValue(objectPath, out GameObject targetObject))
                            {
                                bool currentState = targetObject.activeSelf;
                                currentObjectStates[objectName] = !currentState;
                                stateChanged = true;
                                AddDebugLog($"캐릭터에서 TO 명령어 처리: {command} -> 오브젝트: {objectName}, 상태: {(!currentState ? "활성화" : "비활성화")}");
                            }
                        }

                        // 에셋에서 찾기
                        for (int i = 1; i <= 10; i++)
                        {
                            var asset = GetType().GetField($"asset{i}").GetValue(this) as GameObjectAsset;
                            if (asset != null && asset.Active && enableObjectToggle)
                            {
                                string objectPath = FindObjectPath(objectName, asset.GameObject);
                                if (objectPath != null && objectDict.TryGetValue(objectPath, out GameObject targetObject))
                                {
                                    bool currentState = targetObject.activeSelf;
                                    currentObjectStates[objectName] = !currentState;
                                    stateChanged = true;
                                    AddDebugLog($"에셋 {i}에서 TO 명령어 처리: {command} -> 오브젝트: {objectName}, 상태: {(!currentState ? "활성화" : "비활성화")}");
                                }
                            }
                        }

                        if (!stateChanged)
                        {
                            currentObjectStates[objectName] = true;
                            AddDebugLog($"TO 명령어 감지: {command} -> 오브젝트: {objectName}, 상태: 활성화 (이전 상태 없음)");
                        }
                    }
                }

                var objectsToUpdate = new Dictionary<string, bool>();
                foreach (var kvp in currentObjectStates)
                {
                    if (!_previousObjectStates.TryGetValue(kvp.Key, out bool previousState) || previousState != kvp.Value)
                    {
                        objectsToUpdate[kvp.Key] = kvp.Value;
                        AddDebugLog($"상태 변경 필요: {kvp.Key} -> {(kvp.Value ? "활성화" : "비활성화")} (이전 상태: {(previousState ? "활성화" : "비활성화")})");
                    }
                }

                foreach (var kvp in objectsToUpdate)
                {
                    bool found = false;

                    // 캐릭터에서 찾기
                    if (characterAsset != null && characterAsset.Active && enableObjectToggle)
                    {
                        string objectPath = FindObjectPath(kvp.Key, characterAsset.GameObject);
                        if (objectPath != null && objectDict.TryGetValue(objectPath, out GameObject targetObject))
                        {
                            if (targetObject.activeSelf != kvp.Value)
                            {
                                targetObject.SetActive(kvp.Value);
                                _debugInfo = $"캐릭터 오브젝트 '{kvp.Key}' {(kvp.Value ? "활성화" : "비활성화")} 됨";
                                AddDebugLog($"캐릭터 오브젝트 상태 변경 완료: '{kvp.Key}' -> {(kvp.Value ? "활성화" : "비활성화")}");
                                AddWorkLog($"캐릭터_{kvp.Key}", true);
                                found = true;
                            }
                        }
                    }

                    // 에셋에서 찾기
                    for (int i = 1; i <= 10; i++)
                    {
                        var asset = GetType().GetField($"asset{i}").GetValue(this) as GameObjectAsset;
                        if (asset != null && asset.Active && enableObjectToggle)
                        {
                            string objectPath = FindObjectPath(kvp.Key, asset.GameObject);
                            if (objectPath != null && objectDict.TryGetValue(objectPath, out GameObject targetObject))
                            {
                                if (targetObject.activeSelf != kvp.Value)
                                {
                                    targetObject.SetActive(kvp.Value);
                                    _debugInfo = $"에셋 {i} 오브젝트 '{kvp.Key}' {(kvp.Value ? "활성화" : "비활성화")} 됨";
                                    AddDebugLog($"에셋 {i} 오브젝트 상태 변경 완료: '{kvp.Key}' -> {(kvp.Value ? "활성화" : "비활성화")}");
                                    AddWorkLog($"에셋{i}_{kvp.Key}", true);
                                    found = true;
                                }
                            }
                        }
                    }

                    if (!found)
                    {
                        AddDebugLog($"오브젝트를 찾을 수 없음: {kvp.Key}");
                        AddWorkLog(kvp.Key, false);
                    }
                }

                _previousObjectStates = new Dictionary<string, bool>(currentObjectStates);

                foreach (var command in commands)
                {
                    _currentCommand = command;
                    if (command.StartsWith("BSK_") && IsCommandEnabled(command))
                    {
                        AddDebugLog($"BSK 명령어 처리 시작: {command}");
                        ProcessSingleCommand(command);
                        if (commandDelay > 0)
                        {
                            await Task.Delay(TimeSpan.FromMilliseconds(commandDelay));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                AddDebugLog($"명령어 처리 중 오류 발생: {ex}");
            }
            finally
            {
                _isProcessingCommands = false;
                _currentCommand = "";
                AddDebugLog("모든 명령어 처리 완료");
                InvokeFlow(nameof(Exit));
            }
        }

        private string FindObjectPath(string objectName, GameObject root)
        {
            var transforms = root.GetComponentsInChildren<Transform>(true);
            foreach (var transform in transforms)
            {
                if (transform.name == objectName)
                {
                    return GetFullPath(transform);
                }
            }
            return null;
        }

        private void ProcessSingleCommand(string command)
        {
            bool hasAnyAsset = characterAsset != null;
            for (int i = 1; i <= 10; i++)
            {
                var asset = GetType().GetField($"asset{i}").GetValue(this) as GameObjectAsset;
                if (asset != null && asset.Active)
                {
                    hasAnyAsset = true;
                    break;
                }
            }

            if (!hasAnyAsset)
            {
                _debugInfo = "캐릭터와 에셋이 모두 연결되어 있지 않습니다.";
                AddDebugLog("캐릭터와 에셋이 모두 연결되어 있지 않습니다.");
                return;
            }

            try
            {
                command = Uri.UnescapeDataString(command);

                // BSK_ 명령 처리
                if (command.StartsWith("BSK_"))
                {
                    var (shapeKeyName, value) = ParseShapeKeyCommand(command);
                    if (shapeKeyName != null)
                    {
                        AddDebugLog($"쉐이프키 명령어 파싱 결과 - 이름: {shapeKeyName}, 값: {value}");
                        bool found = false;

                        // 캐릭터 처리
                        if (characterAsset != null && characterAsset.Active && enableShapeKeyControl)
                        {
                            var meshRenderers = characterAsset.GameObject.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                            foreach (var renderer in meshRenderers)
                            {
                                int shapeKeyIndex = renderer.sharedMesh.GetBlendShapeIndex(shapeKeyName);
                                if (shapeKeyIndex != -1)
                                {
                                    float previousWeight = renderer.GetBlendShapeWeight(shapeKeyIndex);
                                    renderer.SetBlendShapeWeight(shapeKeyIndex, value * 100f);
                                    found = true;
                                    AddDebugLog($"캐릭터 쉐이프키 '{shapeKeyName}' 발견 - 렌더러: {renderer.name}");
                                    AddDebugLog($"쉐이프키 값 변경: {previousWeight} -> {value * 100f}");
                                    AddWorkLog($"캐릭터_{shapeKeyName}", true);
                                    break;
                                }
                            }
                        }

                        // 에셋 처리
                        for (int i = 1; i <= 10; i++)
                        {
                            var asset = GetType().GetField($"asset{i}").GetValue(this) as GameObjectAsset;
                            if (asset != null && asset.Active && enableShapeKeyControl)
                            {
                                var meshRenderers = asset.GameObject.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                                foreach (var renderer in meshRenderers)
                                {
                                    int shapeKeyIndex = renderer.sharedMesh.GetBlendShapeIndex(shapeKeyName);
                                    if (shapeKeyIndex != -1)
                                    {
                                        float previousWeight = renderer.GetBlendShapeWeight(shapeKeyIndex);
                                        renderer.SetBlendShapeWeight(shapeKeyIndex, value * 100f);
                                        found = true;
                                        AddDebugLog($"에셋 {i} 쉐이프키 '{shapeKeyName}' 발견 - 렌더러: {renderer.name}");
                                        AddDebugLog($"쉐이프키 값 변경: {previousWeight} -> {value * 100f}");
                                        AddWorkLog($"에셋{i}_{shapeKeyName}", true);
                                        break;
                                    }
                                }
                            }
                        }

                        if (!found)
                        {
                            _debugInfo = $"쉐이프키를 찾을 수 없습니다: {shapeKeyName}";
                            AddDebugLog(_debugInfo);
                            AddWorkLog(shapeKeyName, false);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _debugInfo = $"명령어 처리 중 오류 발생: {ex.Message}";
                AddDebugLog($"명령어 처리 중 오류 발생: {ex}");
            }
        }

        private void UpdateObjectDict(GameObject root)
        {
            try
            {
                objectDict.Clear();
                objectPaths.Clear();
                BuildObjectDict(root, "Character Root");
            }
            catch (Exception ex)
            {
                _debugInfo = $"오브젝트 목록 업데이트 중 오류 발생: {ex.Message}";
                AddDebugLog($"오브젝트 목록 업데이트 중 오류 발생: {ex}");
            }
        }

        private void BuildObjectDict(GameObject obj, string path)
        {
            string currentPath = string.IsNullOrEmpty(path) ? obj.name : path + "/" + obj.name;
            objectDict[currentPath] = obj;
            objectPaths.Add(currentPath);

            foreach (Transform child in obj.transform)
            {
                BuildObjectDict(child.gameObject, currentPath);
            }
        }

        private (string shapeKeyName, float value) ParseShapeKeyCommand(string command)
        {
            try
            {
                string withoutPrefix = command.Substring(4);
                int lastUnderscoreIndex = withoutPrefix.LastIndexOf('_');
                if (lastUnderscoreIndex == -1) return (null, 0f);

                string shapeKeyName = withoutPrefix.Substring(0, lastUnderscoreIndex);
                string valueStr = withoutPrefix.Substring(lastUnderscoreIndex + 1);

                // _OBJ 접미사 제거
                if (shapeKeyName.EndsWith("_OBJ"))
                {
                    shapeKeyName = shapeKeyName.Substring(0, shapeKeyName.Length - 4);
                }

                if (float.TryParse(valueStr, out float value))
                {
                    return (shapeKeyName, value);
                }

                _debugInfo = $"잘못된 값 형식: {valueStr}";
                AddDebugLog(_debugInfo);
                return (null, 0f);
            }
            catch (Exception ex)
            {
                _debugInfo = $"명령어 파싱 중 오류 발생: {ex.Message}";
                AddDebugLog($"명령어 파싱 중 오류 발생: {ex}");
                return (null, 0f);
            }
        }
    }
}
