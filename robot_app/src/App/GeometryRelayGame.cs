using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using KebbiBrain.Hardware;

namespace KebbiBrain.App
{
    // G2《幾何證明接力站》核心邏輯：雙機「說—走—指—接棒」。
    // 乙機(reasoner)宣告每一步理由 → 用 IRobotLink 觸發甲機(guide)走位 + 手臂指向該邊/角 → 甲機回報 DONE → 乙機接下一步。
    // 平板免疫核心：移動到地面大圖對應位置 + 關節手臂指認 + 雙機接力(都是平板做不到的物理能力)。
    // 交棒等待用 LinkAwaiter(送 POINT → await 甲機回 DONE 帶逾時)→ Sim 同步遞送與真機 UDP 非同步遞送皆正確
    //   (舊版「送 POINT 後同步讀旗標」只在 Sim 成立);甲機逾時沒回(離線/卡住)→ 乙機降級口述、不卡死。可重入。
    public sealed class GeometryRelayGame
    {
        private readonly IKebbiBody _guide;     // 甲機：會走位、手臂指認
        private readonly IRobotLink _guideLink;
        private readonly IRobotLink _reasonerLink;
        private readonly IVoice _reasonerVoice; // 乙機：宣告理由
        private readonly LinkAwaiter _awaiter;  // 乙機在自己的 link 上等甲機回 DONE
        private readonly int _doneTimeoutMs;
        private readonly Action<string> _log;

        public int StepsDone { get; private set; }     // 完成的步數
        public int StepsSkipped { get; private set; }   // 甲機逾時沒回 → 降級跳過的步數
        public int Score { get; private set; }          // 學習單作答得分(每步答對邏輯層次 +1;Layer 為 null 的步不計)

        public sealed class Step
        {
            public string Reason;   // 例「因為 AB = AC（已知，等腰）」
            public string Edge;     // 例「AB,AC」
            public float ArmAngle;  // 手臂指向角度
            public string Layer;    // 學習單邏輯層次:已知/因為/所以(null=該步不出學習單題,維持舊行為)
            public Step(string reason, string edge, float armAngle, string layer = null)
            { Reason = reason; Edge = edge; ArmAngle = armAngle; Layer = layer; }
        }

        // doneTimeoutMs：等甲機回 DONE 的上限(過了算甲機離線/卡住 → 降級)。
        public GeometryRelayGame(IKebbiBody guideBody, IRobotLink guideLink, IRobotLink reasonerLink,
                                 IVoice reasonerVoice, Action<string> log, int doneTimeoutMs = 2000)
        {
            _guide = guideBody; _guideLink = guideLink; _reasonerLink = reasonerLink;
            _reasonerVoice = reasonerVoice; _log = log ?? Console.WriteLine; _doneTimeoutMs = doneTimeoutMs;
            _awaiter = new LinkAwaiter(_reasonerLink); // 乙機 link 收 DONE

            // 甲機收到 POINT|edge|angle → 走位 + 手臂指向 + 回 DONE|edge
            _guideLink.OnMessage((from, t) =>
            {
                if (!t.StartsWith("POINT")) return;
                var p = t.Split('|');
                string edge = p[1];
                float angle = float.Parse(p[2], CultureInfo.InvariantCulture);
                _guide.Move(0.1f);        // 走向地面大圖該邊(H201 不動;輪式會走;開迴路)
                _guide.StopWheels();
                _guide.SetMotor(KebbiMotor.RShoulderY, angle); // 手臂指認
                _log("   👉 甲機 走到並指向 " + edge);
                _guideLink.SendAsync(from, "DONE|" + edge);
            });
        }

        public async Task RunProofAsync(List<Step> steps)
        {
            StepsDone = 0; StepsSkipped = 0; Score = 0; // 可重入:每場重置
            foreach (var s in steps)
            {
                await _reasonerVoice.SpeakAsync(s.Reason, "zh-TW");   // 乙機宣告理由

                // 學習單作答(可選:Step.Layer 有值才出題):問學生這步是「已知/因為/所以」,答對計分、答錯念提示。
                if (s.Layer != null)
                {
                    await _reasonerVoice.SpeakAsync("這一步是『已知』、『因為』還是『所以』?", "zh-TW");
                    string heard = await _reasonerVoice.ListenAsync("zh-TW");
                    if (!string.IsNullOrEmpty(heard) && heard.Contains(s.Layer))
                    { Score++; _log("   ✅ 學習單答對：這步是「" + s.Layer + "」（得分 " + Score + "）"); }
                    else
                    {
                        await _reasonerVoice.SpeakAsync("再想想：這步是在『用已知條件』、『推論過程』還是『下結論』?", "zh-TW");
                        _log("   ✏ 學習單答錯（學生說「" + heard + "」）→ 正解是「" + s.Layer + "」，乙機提示後繼續");
                    }
                }

                // 鐵則:先註冊 DONE 等待者,再送 POINT(Sim 同步遞送下 DONE 會在 SendAsync 當下就回,先送會漏接)。
                var doneTask = _awaiter.WaitForAsync((f, t) => t == "DONE|" + s.Edge, _doneTimeoutMs);
                await _reasonerLink.SendAsync(_guideLink.RobotId, "POINT|" + s.Edge + "|" + s.ArmAngle.ToString(CultureInfo.InvariantCulture));
                if (await doneTask != null)
                {
                    StepsDone++;
                    _log("   ✔ 第 " + StepsDone + " 步完成：「" + s.Reason + "」→ 指認 " + s.Edge);
                }
                else
                {
                    StepsSkipped++;
                    _log("   ⚠ 甲機逾時沒回（離線/卡住）→ 乙機降級口述、不卡死，繼續下一步");
                }
            }
        }

        // 內建範例證明：等腰三角形「兩底角相等」的 3 步接力。
        public static List<Step> MakeIsoscelesProof()
        {
            return new List<Step>
            {
                new Step("因為 AB = AC（已知，等腰三角形）", "AB,AC", 60f),
                new Step("作頂角平分線 AD，∠BAD = ∠CAD", "AD", 30f),
                new Step("所以 △ABD ≅ △ACD（SAS），得 ∠B = ∠C", "△ABD,△ACD", 80f),
            };
        }

        // 學習單版:同一證明,但每步標上邏輯層次(已知/因為/所以)→ 乙機會問學生這步屬哪一層、答對計分。
        public static List<Step> MakeIsoscelesProofWorksheet()
        {
            return new List<Step>
            {
                new Step("因為 AB = AC（已知，等腰三角形）", "AB,AC", 60f, "已知"),
                new Step("作頂角平分線 AD，∠BAD = ∠CAD", "AD", 30f, "因為"),
                new Step("所以 △ABD ≅ △ACD（SAS），得 ∠B = ∠C", "△ABD,△ACD", 80f, "所以"),
            };
        }

        // 三角形內角和 = 180° 的 3 步證明(過頂點作底邊平行線 → 內錯角 → 平角)。
        public static List<Step> MakeAngleSumProof()
        {
            return new List<Step>
            {
                new Step("過 A 作 BC 的平行線 ℓ（輔助線）", "ℓ", 50f, "已知"),
                new Step("因為 ℓ∥BC，內錯角相等：∠1 = ∠B、∠2 = ∠C", "∠1,∠2", 30f, "因為"),
                new Step("所以 ∠A + ∠B + ∠C = 平角 = 180°", "∠A,∠B,∠C", 80f, "所以"),
            };
        }

        // 三角形外角定理:外角 = 兩不相鄰內角和。
        public static List<Step> MakeExteriorAngleProof()
        {
            return new List<Step>
            {
                new Step("∠ACD 是 △ABC 在 C 的外角，與 ∠ACB 互補（已知）", "∠ACD", 50f, "已知"),
                new Step("因為 ∠A + ∠B + ∠ACB = 180°（內角和）", "∠A,∠B", 35f, "因為"),
                new Step("所以 ∠ACD = ∠A + ∠B", "∠ACD,∠A,∠B", 80f, "所以"),
            };
        }

        // 具名證明(供換題)。
        public sealed class Proof
        {
            public string Title; public List<Step> Steps;
            public Proof(string title, List<Step> steps) { Title = title; Steps = steps; }
        }

        // 證明題庫:讓課堂/展演可換題(每題皆學習單版,含邏輯層次)。
        public static List<Proof> MakeProofLibrary()
        {
            return new List<Proof>
            {
                new Proof("等腰三角形兩底角相等", MakeIsoscelesProofWorksheet()),
                new Proof("三角形內角和 180°", MakeAngleSumProof()),
                new Proof("三角形外角定理", MakeExteriorAngleProof()),
            };
        }

        // ── G2 學生自編走位腳本驗證(純加法、向後相容)──────────────────────────
        // 手冊命脈:學生把證明步驟序列(每步含邏輯層次 Layer=已知/因為/所以)排出來,
        // ValidateScript 先「對結構把關」(順序錯置 / 缺步 / 多步 / 邏輯層次錯置 / 缺標層),
        // 指出「第幾步」哪種錯;只有通過(Ok)才放行 RunProofAsync 接力。
        // 純加法:新增 enum/巢狀型別/靜態純函式/便利重載,完全不動既有建構式、欄位與 RunProofAsync 簽章。

        // 結構錯誤分類(對應手冊「順序/缺步/邏輯層次錯置」)。
        public enum ScriptError { None, MissingStep, ExtraStep, WrongOrder, WrongLayer, MissingLayer }

        // 驗證結果:Ok + 第幾步(1-based;缺步用「應有第 N 步但沒有」)+ 人類可讀訊息。
        public sealed class ScriptCheck
        {
            public bool Ok;            // true=結構正確,可 RunProof
            public ScriptError Error;  // 錯誤種類
            public int Step;           // 出錯步號(1-based);0=整體缺步/無步驟
            public string Message;     // 例「第 2 步邏輯層次錯置:應為『因為』,實際標成『所以』」
            public ScriptCheck(bool ok, ScriptError e, int step, string msg)
            { Ok = ok; Error = e; Step = step; Message = msg; }
            public static ScriptCheck Pass() { return new ScriptCheck(true, ScriptError.None, 0, "結構正確,可開始接力"); }
        }

        // 邏輯層次的單調排序索引:已知=0 因為=1 所以=2(其餘=-1 視為缺/錯層)。
        private static int LayerRank(string layer)
        {
            return layer == "已知" ? 0 : layer == "因為" ? 1 : layer == "所以" ? 2 : -1;
        }

        // 純函式 validator:逐步比對 student 與 expected(標準解)的 Layer 序列。
        // 規則:
        //   (a) student==null → MissingStep(整體沒步驟)。
        //   (b) 步數須與標準解一致:少=MissingStep(指缺的第一步) / 多=ExtraStep(指多出的那步)。
        //   (c) 逐步:每步須有合法 Layer(否則 MissingLayer);
        //       層次不可從高倒退回低(已知→因為→所以 單調遞增,否則 WrongOrder);
        //       對應位置 Layer 須與標準解相同(否則 WrongLayer)。
        public static ScriptCheck ValidateScript(List<Step> student, List<Step> expected)
        {
            if (student == null)
                return new ScriptCheck(false, ScriptError.MissingStep, 0, "沒有任何步驟");
            int expectedCount = expected == null ? 0 : expected.Count;

            // (b) 缺步 / 多步(以標準解步數為準)。
            if (student.Count < expectedCount)
                return new ScriptCheck(false, ScriptError.MissingStep, student.Count + 1,
                    "缺步:應有 " + expectedCount + " 步,只給了 " + student.Count + " 步(缺第 " + (student.Count + 1) + " 步起)");
            if (student.Count > expectedCount)
                return new ScriptCheck(false, ScriptError.ExtraStep, expectedCount + 1,
                    "多步:標準解 " + expectedCount + " 步,第 " + (expectedCount + 1) + " 步是多餘的");

            // (c) 逐步:層次必須有、且與標準解一致;同時偵測層次逆序。
            int prev = -1;
            for (int i = 0; i < student.Count; i++)
            {
                string sl = student[i].Layer;
                if (sl == null || LayerRank(sl) < 0)
                    return new ScriptCheck(false, ScriptError.MissingLayer, i + 1,
                        "第 " + (i + 1) + " 步未標邏輯層次(需『已知/因為/所以』)");
                int r = LayerRank(sl);
                if (r < prev)  // 例:第 2 步『所以』後第 3 步又回『因為』→ 層次逆序
                    return new ScriptCheck(false, ScriptError.WrongOrder, i + 1,
                        "第 " + (i + 1) + " 步順序錯置:邏輯層次不可從『" + LayerLabel(prev) + "』倒退回『" + sl + "』");
                string el = expected[i].Layer;
                if (sl != el)
                    return new ScriptCheck(false, ScriptError.WrongLayer, i + 1,
                        "第 " + (i + 1) + " 步邏輯層次錯置:應為『" + el + "』,實際標成『" + sl + "』");
                prev = r;
            }
            return ScriptCheck.Pass();
        }

        private static string LayerLabel(int rank)
        {
            return rank == 0 ? "已知" : rank == 1 ? "因為" : rank == 2 ? "所以" : "(未標)";
        }

        // 便利重載:驗證通過才接力,並回報結果(demo/呼叫端用)。
        // 不過 → 不跑 RunProofAsync(StepsDone 維持 0),只 log 提示;回傳 ScriptCheck 供呼叫端判斷。
        public async Task<ScriptCheck> RunProofIfValidAsync(List<Step> student, List<Step> expected)
        {
            var chk = ValidateScript(student, expected);
            if (chk.Ok) await RunProofAsync(student);   // 通過才執行既有接力
            else _log("   ⛔ 腳本未通過驗證:" + chk.Message + "(請改好再接力)");
            return chk;
        }
    }
}
