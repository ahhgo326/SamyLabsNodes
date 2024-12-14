// VERSION 0.0.3
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
        Title = "Object Controller v0.0.3",
        Category = "SamyLabs",
        Width = 1.5f
    )]
    public class ObjectControllerNode : Warudo.Core.Graphs.Node
    {
        [DataInput]
        [Label("Character")]
        public GameObjectAsset characterAsset;

        // :q :q :q :q :q :q :q :q :q :q :q
        [DataInput]
        [Label("Asset")]
        public GameObjectAsset asset1;

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
        [Label("Toggle Control")] 
        public bool enableObjectToggle = true; 

        [DataInput]
        [Label("ON/OFF Control")]
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
            base.OnCreate();

            Watch<GameObjectAsset>(nameof(characterAsset), (from, to) =>
            {
                ResetState();
                if (to?.GameObject != null && to.Active)
                {
                    InitializeAssets();
                }
            });

            Watch<GameObjectAsset>(nameof(asset1), (from, to) =>
            {
                ResetState();
                if (to?.GameObject != null && to.Active)
                {
                    InitializeAssets();
                }
            });

            WatchAsset(nameof(characterAsset), () =>
            {
                ResetState();
                if (characterAsset?.GameObject != null && characterAsset.Active)
                {
                    InitializeAssets();
                }
            });

            WatchAsset(nameof(asset1), () =>
            {
                ResetState();
                if (asset1?.GameObject != null && asset1.Active)
                {
                    InitializeAssets();
                }
            });
        }

        private void ResetState()
        {
            rendererDict.Clear();
            rendererShapeKeys.Clear();
            _previousObjectStates.Clear();
            _workLogs.Clear();
            _debugLogs.Clear();
            objectDict.Clear();
            objectPaths.Clear();
        }

        private void InitializeAssets()
        {
            try
            {
                if (characterAsset?.GameObject != null)
                {
                    UpdateObjectDict(characterAsset.GameObject);
                    UpdateRendererDict(characterAsset.GameObject);
                    AddDebugLog($"캐릭터 '{characterAsset.Name}' 초기화 완료");
                }

                if (asset1?.GameObject != null)
                {
                    UpdateObjectDict(asset1.GameObject);
                    UpdateRendererDict(asset1.GameObject);
                    AddDebugLog($"에셋 '{asset1.Name}' 초기화 완료");
                }
            }
            catch (Exception ex)
            {
                AddDebugLog($"에셋 초기화 중 오류 발생: {ex.Message}");
            }
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
            if (transform == null) return "";
            
            List<string> pathParts = new List<string>();
            Transform current = transform;
            GameObject characterRoot = characterAsset?.GameObject;
            GameObject assetRoot = asset1?.GameObject;
            
            while (current != null)
            {
                // characterAsset이나 asset1의 GameObject에 도달하면 중지
                if ((characterRoot != null && current.gameObject == characterRoot) ||
                    (assetRoot != null && current.gameObject == assetRoot))
                {
                    break;
                }
                pathParts.Add(current.name);
                current = current.parent;
            }
            
            pathParts.Reverse();
            string fullPath = string.Join("/", pathParts);
            AddDebugLog($"GetFullPath - 변환된 경로: {fullPath} (원본 오브젝트: {transform.name})");
            return fullPath;
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
                        currentObjectStates[objectName] = prefix == "ON_";
                        AddDebugLog($"ON/OFF 명령어 감지: {command} -> 오브젝트: {objectName}, 상태: {(prefix == "ON_" ? "활성화" : "비활성화")}");
                    }
                    else if (command.StartsWith("TO_") && IsCommandEnabled(command))
                    {
                        string objectName = command.Substring(3);
                        bool foundInCharacter = false;
                        bool foundInAsset = false;

                        // 캐릭터에서 찾기
                        if (characterAsset != null && characterAsset.Active && enableObjectToggle)
                        {
                            var objectPaths = FindObjectPath(objectName, characterAsset.GameObject);
                            if (objectPaths != null && objectPaths.Count > 0)
                            {
                                foreach (var path in objectPaths)
                                {
                                    if (objectDict.TryGetValue(path, out GameObject targetObject))
                                    {
                                        bool currentState = targetObject.activeSelf;
                                        currentObjectStates[objectName] = !currentState;
                                        foundInCharacter = true;
                                        AddDebugLog($"캐릭터에서 TO 명령어 처리: {command} -> 오브젝트: {objectName}, 상태: {(!currentState ? "활성화" : "비활성화")}");
                                    }
                                }
                            }
                        }

                        // 에셋에서 찾기
                        if (asset1 != null && asset1.Active && enableObjectToggle)
                        {
                            var objectPaths = FindObjectPath(objectName, asset1.GameObject);
                            if (objectPaths != null && objectPaths.Count > 0)
                            {
                                foreach (var path in objectPaths)
                                {
                                    if (objectDict.TryGetValue(path, out GameObject targetObject))
                                    {
                                        bool currentState = targetObject.activeSelf;
                                        currentObjectStates[objectName] = !currentState;
                                        foundInAsset = true;
                                        AddDebugLog($"에셋에서 TO 명령어 처리: {command} -> 오브젝트: {objectName}, 상태: {(!currentState ? "활성화" : "비활성화")}");
                                    }
                                }
                            }
                        }

                        if (!foundInCharacter && !foundInAsset)
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
                    bool foundInCharacter = false;
                    bool foundInAsset = false;

                    // 캐릭터에서 찾기
                    if (characterAsset != null && characterAsset.Active && enableObjectToggle)
                    {
                        var objectPaths = FindObjectPath(kvp.Key, characterAsset.GameObject);
                        if (objectPaths != null && objectPaths.Count > 0)
                        {
                            foreach (var path in objectPaths)
                            {
                                if (objectDict.TryGetValue(path, out GameObject targetObject))
                                {
                                    if (targetObject.activeSelf != kvp.Value)
                                    {
                                        targetObject.SetActive(kvp.Value);
                                        _debugInfo = $"캐릭터 오브젝트 '{kvp.Key}' {(kvp.Value ? "활성화" : "비활성화")} 됨";
                                        AddDebugLog($"캐릭터 오브젝트 상태 변경 완료: '{kvp.Key}' -> {(kvp.Value ? "활성화" : "비활성화")}");
                                        AddWorkLog($"캐릭터_{kvp.Key}", true);
                                        foundInCharacter = true;
                                    }
                                }
                            }
                        }
                    }

                    // 에셋에서 찾기
                    if (asset1 != null && asset1.Active && enableObjectToggle)
                    {
                        var objectPaths = FindObjectPath(kvp.Key, asset1.GameObject);
                        if (objectPaths != null && objectPaths.Count > 0)
                        {
                            foreach (var path in objectPaths)
                            {
                                if (objectDict.TryGetValue(path, out GameObject targetObject))
                                {
                                    if (targetObject.activeSelf != kvp.Value)
                                    {
                                        targetObject.SetActive(kvp.Value);
                                        _debugInfo = $"에셋 오브젝트 '{kvp.Key}' {(kvp.Value ? "활성화" : "비활성화")} 됨";
                                        AddDebugLog($"에셋 오브젝트 상태 변경 완료: '{kvp.Key}' -> {(kvp.Value ? "활성화" : "비활성화")}");
                                        AddWorkLog($"에셋_{kvp.Key}", true);
                                        foundInAsset = true;
                                    }
                                }
                            }
                        }
                    }

                    if (!foundInCharacter && !foundInAsset)
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

        private List<string> FindObjectPath(string objectName, GameObject root)
        {
            AddDebugLog($"검색할 오브젝트 이름: {objectName}");
            var transforms = root.GetComponentsInChildren<Transform>(true);
            var matchingPaths = new List<string>();
            
            // objectDict에 저장된 모든 경로 출력
            AddDebugLog("현재 objectDict에 저장된 경로들:");
            foreach (var path in objectDict.Keys)
            {
                AddDebugLog($"- {path}");
            }

            foreach (var transform in transforms)
            {
                string fullPath = GetFullPath(transform);
                AddDebugLog($"검색된 경로: {fullPath}");
                
                if (transform.name == objectName || fullPath.EndsWith(objectName))
                {
                    if (objectDict.ContainsKey(fullPath))
                    {
                        AddDebugLog($"일치하는 오브젝트 찾음 (Dict에 존재): {fullPath}");
                        matchingPaths.Add(fullPath);
                    }
                    else
                    {
                        AddDebugLog($"오브젝트를 찾았으나 Dict에 없음: {fullPath}");
                        // Dict에 추가
                        objectDict[fullPath] = transform.gameObject;
                        matchingPaths.Add(fullPath);
                    }
                }
            }
            
            if (matchingPaths.Count == 0)
            {
                AddDebugLog($"오브젝트를 찾을 수 없음: {objectName}");
            }
            else
            {
                AddDebugLog($"총 {matchingPaths.Count}개의 일치하는 오브젝트를 찾음");
            }
            
            return matchingPaths;
        }

        private void ProcessSingleCommand(string command)
        {
            bool hasAnyAsset = characterAsset != null;
            if (asset1 != null && asset1.Active)
            {
                hasAnyAsset = true;
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
                        bool foundCharacter = false;
                        bool foundAsset = false;

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
                                    foundCharacter = true;
                                    AddDebugLog($"캐릭터 쉐이프키 '{shapeKeyName}' 발견 - 렌더러: {renderer.name}");
                                    AddDebugLog($"쉐이프키 값 변경: {previousWeight} -> {value * 100f}");
                                    AddWorkLog($"캐릭터_{shapeKeyName}", true);
                                }
                            }
                        }

                        // 에셋 처리
                        if (asset1 != null && asset1.Active && enableShapeKeyControl)
                        {
                            var meshRenderers = asset1.GameObject.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                            foreach (var renderer in meshRenderers)
                            {
                                int shapeKeyIndex = renderer.sharedMesh.GetBlendShapeIndex(shapeKeyName);
                                if (shapeKeyIndex != -1)
                                {
                                    float previousWeight = renderer.GetBlendShapeWeight(shapeKeyIndex);
                                    renderer.SetBlendShapeWeight(shapeKeyIndex, value * 100f);
                                    foundAsset = true;
                                    AddDebugLog($"에셋 쉐이프키 '{shapeKeyName}' 발견 - 렌더러: {renderer.name}");
                                    AddDebugLog($"쉐이프키 값 변경: {previousWeight} -> {value * 100f}");
                                    AddWorkLog($"에셋_{shapeKeyName}", true);
                                }
                            }
                        }

                        if (!foundCharacter && !foundAsset)
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
