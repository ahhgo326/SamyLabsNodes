// VERSION 0.0.1
using UnityEngine;
using System;
using System.Collections.Generic;
using Warudo.Core.Attributes;
using Warudo.Core.Graphs;

namespace sami6.Donation
{
    [NodeType(
        Id = "com.sami6.DonationProcessor",
        Title = "Donation Processor v0.0.1",
        Category = "SamyLabs"
    )]
    public class DonationProcessorNode : Node
    {
        private List<string> _debugLogs = new List<string>();

        private void AddDebugLog(string message)
        {
            _debugLogs.Add($"[{DateTime.Now:HH:mm:ss}] {message}");
            if (_debugLogs.Count > 100)
            {
                _debugLogs.RemoveAt(0);
            }
        }

        [DataInput]
        [Label("Donation Info")]
        public string[] donation;

        [DataOutput]
        [Label("플랫폼 / Platform (str)")]
        public string GetProvider()
        {
            return donation?.Length > 0 ? donation[0] : null;
        }

        [DataOutput]
        [Label("닉네임 / Nickname (str)")]
        public string GetUserNickname()
        {
            return donation?.Length > 1 ? donation[1] : null;
        }

        [DataOutput]
        [Label("타입 / Type (str)")]
        public new string GetType()
        {
            return donation?.Length > 2 ? donation[2] : null; 
        }

        [DataOutput]
        [Label("금액 / Amount (int)")]
        public int GetAmount()
        {
            if (donation?.Length > 3 && !string.IsNullOrEmpty(donation[3]))
            {
                if (int.TryParse(donation[3], out int amount))
                {
                    return amount;
                }
            }
            return 0;
        }

        [DataOutput]
        [Label("메시지 / Message (str)")]
        public string GetMessage()
        {
            return donation?.Length > 4 ? donation[4] : null;
        }

        [DataOutput]
        [Label("로그 / Logs")]
        public string[] GetDebugLogs()
        {
            return _debugLogs.ToArray();
        }

        [FlowInput]
        [Label("Enter")]
        public Continuation Process()
        {
            if (donation != null && donation.Length >= 5)
            {
                AddDebugLog($"Platform: {GetProvider()} | Nickname: {GetUserNickname()} | Type: {GetType()} | Amount: {GetAmount()} | Message: {GetMessage()}");
                return Exit;
            }
            return null;
        }

        [FlowOutput]
        [Label("Exit")]
        public Continuation Exit;
    }
}
