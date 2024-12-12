// VERSION 0.0.1
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using Warudo.Core.Attributes;
using Warudo.Core.Graphs;
using Warudo.Core.Data;

namespace sami6.Manual
{
    [NodeType(
        Id = "sami6.manual.ManualCommandProcessor", 
        Title = "Manual Controller v0.0.1", 
        Category = "SamyLabs")]
    public class ManualCommandProcessorNode : Node
    {
        [DataInput]
        [Label("명령어 데이터 / Commands data (str[])")]
        public string[] commands;

        [DataInput]
        [Label("매뉴얼 문자열 / Match Strings (str[])")]
        public string[] matchStrings;

        private Dictionary<string, string> manualExits = new Dictionary<string, string>();

        protected override void OnCreate()
        {
            base.OnCreate();
            UpdateExits();
            
            // matchStrings 데이터 입력이 변경될 때마다 UpdateExits 호출
            Watch<string[]>(nameof(matchStrings), (oldValue, newValue) => UpdateExits());
        }

        private void UpdateExits()
        {
            // 기존 Exit 포트들을 모두 제거 (기본 Exit과 Unmatched 제외)
            var ports = FlowOutputPortCollection.GetPorts().ToList();
            foreach (var port in ports)
            {
                if (port.Key != "Exit" && port.Key != "Unmatched") // 기본 Exit과 Unmatched는 유지
                {
                    FlowOutputPortCollection.RemovePort(port.Key);
                }
            }
            manualExits.Clear();

            // Unmatched Exit이 없으면 생성
            if (!FlowOutputPortCollection.GetPorts().Any(p => p.Key == "Unmatched"))
            {
                AddFlowOutputPort("Unmatched");
            }

            // 매뉴얼 문자열 배열의 Exit 생성
            if (matchStrings != null && matchStrings.Length > 0)
            {
                foreach (var match in matchStrings)
                {
                    if (!string.IsNullOrWhiteSpace(match))
                    {
                        var portKey = match.Trim();
                        AddFlowOutputPort(portKey, new FlowOutputProperties 
                        {
                            label = portKey
                        });
                        manualExits[portKey] = portKey;
                    }
                }
            }

            Broadcast();
        }

        [FlowInput]
        [Label("Enter")]
        public Continuation Enter()
        {
            // 기본 Exit은 항상 실행
            InvokeFlow(nameof(Exit));
            
            if (commands != null && commands.Length > 0)
            {
                // 명령어에서 MA_ 접두사를 제거하고 매뉴얼 문자열과 일치하는지 확인
                foreach (var command in commands)
                {
                    string cleanCommand = command.StartsWith("MA_") ? command.Substring(3) : command;
                    if (manualExits.ContainsKey(cleanCommand))
                    {
                        // 일치하는 매뉴얼 문자열의 Exit으로 flow를 보냄
                        InvokeFlow(manualExits[cleanCommand]);
                        return null;
                    }
                }
                // 일치하는 매뉴얼 문자열이 없으면 Unmatched Exit으로 flow를 보냄
                InvokeFlow("Unmatched");
            }
            else
            {
                // 명령어 배열이 비어있으면 Unmatched Exit으로 flow를 보냄
                InvokeFlow("Unmatched");
            }

            return null;
        }

        [FlowOutput]
        [Label("DefaultExit")]
        public Continuation Exit;

        [FlowOutput]
        [Label("ElseExit")]
        public Continuation Unmatched; 
    }
}
