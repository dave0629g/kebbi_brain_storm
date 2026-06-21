using System;
using System.Collections.Generic;

namespace KebbiBrain.App.Counselor
{
    // 可熱重載的安全閘:持有「可換的」SafetyGateCore。CounselorSession 拿到穩定參照,底下規則可由
    // 外部設定檔(輔導老師維護的 counselor_rules.json)即時換掉,免重建/重啟。
    // 安全鐵律:重載失敗(例外/空規則)→ 保留現有規則,絕不讓安全閘變成「無守門」。內容仍須由輔導專業定稿。
    public sealed class ReloadableSafetyGate : ISafetyGate
    {
        private readonly Func<IReadOnlyList<SafetyRule>> _load;
        private SafetyGateCore _core;
        public string LastReloadStatus { get; private set; }

        public ReloadableSafetyGate(Func<IReadOnlyList<SafetyRule>> load)
        {
            _load = load ?? (() => new List<SafetyRule>());
            IReadOnlyList<SafetyRule> initial;
            try { initial = _load() ?? new List<SafetyRule>(); }
            catch { initial = new List<SafetyRule>(); }
            _core = new SafetyGateCore(initial);
            LastReloadStatus = "初次載入:" + _core.Rules.Count + " 條規則";
        }

        public IReadOnlyList<SafetyRule> Rules => _core.Rules;
        public GateResult Evaluate(string studentText) => _core.Evaluate(studentText);

        // 重載:成功才換規則;失敗(例外或空)保留舊規則(寧可用舊的,不可無守門)。回傳是否成功。
        public bool TryReload(out string status)
        {
            try
            {
                var rules = _load();
                if (rules == null || rules.Count == 0)
                { status = LastReloadStatus = "重載失敗:規則為空 → 保留原 " + _core.Rules.Count + " 條"; return false; }
                _core = new SafetyGateCore(rules);
                status = LastReloadStatus = "重載成功:" + rules.Count + " 條規則生效";
                return true;
            }
            catch (Exception e)
            {
                status = LastReloadStatus = "重載失敗(" + e.Message + ")→ 保留原 " + _core.Rules.Count + " 條";
                return false;
            }
        }

        public void Reload() { TryReload(out _); }   // ISafetyGate 介面:重載(忽略回傳)
    }
}
